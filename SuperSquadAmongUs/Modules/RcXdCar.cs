using System.Collections;
using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Networking;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using Reactor.Utilities;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Options.Roles.Impostor;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs.Assets;
using TownOfUs.Events;
using TownOfUs.Modifiers;
using TownOfUs.Networking;
using TownOfUs.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// The RC-XD's car. Spawned/moved/despawned via RPC on every client; only the deployer's client
/// simulates physics and computes detonation kills.
/// </summary>
public static class RcXdCar
{
    internal static GameObject? ActiveCar { get; private set; }

    internal static RcXdCarBehaviour? ActiveBehaviour { get; private set; }

    // Set the frame a self-detonate kills the deployer; read by SuppressHauntAfterSelfDetonatePatch.
    internal static int SelfDetonationFrame { get; private set; } = -1;

    [MethodRpc((uint)SuperSquadRpc.DeployRcXdCar, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcDeployCar(PlayerControl owner, float x, float y)
    {
        if (!AbilityGrants.SenderIsOrHolds<RcXdRole>(owner))
        {
            Error("RpcDeployCar - Invalid RC-XD");
            return;
        }

        DestroyLocally();

        var carObject = new GameObject("SuperSquadRcXdCar");
        carObject.transform.position = new Vector3(x, y, y / 1000f);

        var renderer = carObject.AddComponent<SpriteRenderer>();
        renderer.sprite = SuperSquadImpAssets.RcXdCarSprite.LoadAsset();

        var behaviour = carObject.AddComponent<RcXdCarBehaviour>();
        behaviour.Initialize(owner);

        ActiveCar = carObject;
        ActiveBehaviour = behaviour;
    }

    // No RcXdRole guard here either (see RpcDespawnCar) - the car's own FixedUpdate can still be
    // mid-flight the same tick the owner dies, before the despawn cleanup below has run.
    [MethodRpc((uint)SuperSquadRpc.MoveRcXdCar, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcMoveCar(PlayerControl owner, float x, float y)
    {
        if (ActiveBehaviour == null)
        {
            return;
        }

        if (owner.AmOwner)
        {
            return;
        }

        ActiveBehaviour.SetTargetPosition(new Vector2(x, y));
    }

    [MethodRpc((uint)SuperSquadRpc.DetonateRcXdCar, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcDetonateCar(PlayerControl owner, float x, float y)
    {
        if (!AbilityGrants.SenderIsOrHolds<RcXdRole>(owner))
        {
            Error("RpcDetonateCar - Invalid RC-XD");
            return;
        }

        var options = OptionGroupSingleton<RcXdOptions>.Instance;
        var explosionPos = new Vector3(x, y, y / 1000f);

        TouAudio.PlaySound(TouAudio.ArsoIgniteSound);

        var sphere = MiscUtils.CreateSpherePrimitive(explosionPos, options.ExplosionRadius.Value);
        sphere.GetComponent<MeshRenderer>().material = AuAvengersAnims.IgniteMaterial.LoadAsset();
        Coroutines.Start(DestroyAfterDelay(sphere, 0.4f));

        // RpcSpecialMultiMurder is itself an RPC, so only the deployer computes and sends it once.
        if (owner.AmOwner)
        {
            var targetPos = new Vector2(x, y);
            var radius = options.ExplosionRadius.Value * ShipStatus.Instance.MaxLightRadius;
            var victims = Helpers.GetClosestPlayers(targetPos, radius);

            var filtered = new List<PlayerControl>();
            foreach (var player in victims)
            {
                if (player == null || player.Data == null || player.Data.IsDead || player.Data.Disconnected || player.inVent)
                {
                    continue;
                }

                if (!options.CanKillImpostors && player.IsImpostorAligned())
                {
                    continue;
                }

                if (player.HasModifier<FirstDeadShield>() ||
                    player.GetModifiers<DisabledModifier>().Any(x => !x.CanBeInteractedWith))
                {
                    continue;
                }

                filtered.Add(player);
            }

            if (filtered.Count > 0)
            {
                // The deployer's own death needs handling RpcSpecialMultiMurder doesn't give its
                // source (death-handler bookkeeping, a synchronous kill, no kill-stinger overlay -
                // see below), so it's split out through single-target RpcCustomMurder instead.
                var others = filtered.Where(p => p.PlayerId != owner.PlayerId).ToList();
                var deployerDies = others.Count != filtered.Count;

                if (others.Count > 0)
                {
                    owner.RpcSpecialMultiMurder(others, true, teleportMurderer: false, playKillSound: true,
                        causeOfDeath: "SuperSquadRcXd");
                }

                if (deployerDies)
                {
                    // Deploy/Detonate share the vanilla Ability keybind (Keybinds.SecondaryAction),
                    // which AbilityButton.DoClick() polls independently each frame. Once this kill
                    // flips Data.Role to a ghost, that same still-down key resolves to the ghost's
                    // Haunt ability later this same frame - see SuppressHauntAfterSelfDetonatePatch.
                    SelfDetonationFrame = Time.frameCount;

                    // TOU's death-handler bookkeeping doesn't run for MiraAPI's RpcCustomMurder, so
                    // the deployer's cause of death is set by hand here (an RPC, since only the
                    // deployer's client computes the blast). killedBy: owner, not null - TOU
                    // suppresses the "killed by" text when killedBy == player, and unlike null it's
                    // safe to dereference on the locale-miss branch.
                    DeathHandlerModifier.RpcUpdateLocalDeathHandler(owner, owner, "DiedToSuperSquadRcXd",
                        DeathEventHandlers.CurrentRound, DeathHandlerOverride.SetTrue, "null",
                        DeathHandlerOverride.SetTrue);

                    // teleportMurderer: false keeps the kill synchronous (MiraAPI defaults to true,
                    // which yields for a blur animation) - required by OnEffectEnd's restore-before-
                    // detonate flow. showKillAnim: false avoids vanilla's broken self-kill overlay
                    // (Patches/SelfKillOverlayPatch.cs).
                    owner.RpcCustomMurder(owner, teleportMurderer: false, showKillAnim: false);
                }

                Info($"RC-XD car detonated: killed {filtered.Count} players (deployer died: {deployerDies})");
            }
        }

        DestroyLocally();
    }

    [MethodRpc((uint)SuperSquadRpc.DespawnRcXdCar, LocalHandling = RpcLocalHandling.Before)]
    // Unlike Deploy/Detonate, deliberately no RcXdRole guard here: RcXdDeployButton.FixedUpdate's
    // death-cancel path calls this AFTER the owner's role has already swapped to a ghost.
    public static void RpcDespawnCar(PlayerControl owner)
    {
        DestroyLocally();
    }

    private static void DestroyLocally()
    {
        if (ActiveCar != null)
        {
            // Rescue the local player's light/camera off the car before destroying it, on every
            // destroy path - destroying a GameObject the light is parented to blacks out the screen
            // permanently (docs/il2cpp-gotchas.md).
            var local = PlayerControl.LocalPlayer;
            if (local != null && local.lightSource != null &&
                local.lightSource.transform.parent == ActiveCar.transform)
            {
                local.lightSource.transform.parent = local.transform;
                local.lightSource.transform.localPosition = Vector3.zero;
                local.lightSource.Initialize(local.Collider.offset / 2f);
            }

            if (local != null && HudManager.InstanceExists && ActiveBehaviour != null &&
                HudManager.Instance.PlayerCam.Target == ActiveBehaviour)
            {
                HudManager.Instance.PlayerCam.SetTarget(local);
            }

            Object.Destroy(ActiveCar);
        }

        ActiveCar = null;
        ActiveBehaviour = null;
    }

    /// <summary>
    /// Safety-net cleanup for a meeting starting mid-drive, callable from the car behaviour itself.
    /// </summary>
    internal static void EnsureDestroyedLocally() => DestroyLocally();

    private static IEnumerator DestroyAfterDelay(GameObject sphere, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (sphere != null)
        {
            Object.Destroy(sphere);
        }
    }
}
