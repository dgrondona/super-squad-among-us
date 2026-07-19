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

    [MethodRpc((uint)SuperSquadRpc.DeployRcXdCar, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcDeployCar(PlayerControl owner, float x, float y)
    {
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
                // TOU's multi-murder pipeline hard-crashes when the source is one of its own
                // targets, so the deployer's self-kill goes through the single-target
                // RpcCustomMurder instead - the exact call the Sheriff's misfire uses, defaults
                // and all (docs/il2cpp-gotchas.md).
                var others = filtered.Where(p => p.PlayerId != owner.PlayerId).ToList();
                var deployerDies = others.Count != filtered.Count;

                if (others.Count > 0)
                {
                    owner.RpcSpecialMultiMurder(others, true, teleportMurderer: false, playKillSound: true,
                        causeOfDeath: "SuperSquadRcXd");
                }

                if (deployerDies)
                {
                    // TOU's death-handler bookkeeping doesn't run for MiraAPI's RpcCustomMurder,
                    // leaving the deployer's cause of death generic. Set it on every client (the
                    // Astral does this locally in DieFromFailedReturn; here it must be an RPC
                    // because only the deployer's client computes the blast).
                    // killedBy: owner (not null) - TOU suppresses the "killed by" line when
                    // killedBy == player, and unlike null it can't be dereferenced on the
                    // locale-miss branch.
                    DeathHandlerModifier.RpcUpdateLocalDeathHandler(owner, owner, "DiedToSuperSquadRcXd",
                        DeathEventHandlers.CurrentRound, DeathHandlerOverride.SetTrue, "null",
                        DeathHandlerOverride.SetTrue);

                    // teleportMurderer MUST be explicit false: MiraAPI's default is true, which
                    // makes CoPerformCustomKill yield mid-kill to play a blur animation on the
                    // fresh ghost with the camera locked. The restore-before-detonate flow in
                    // RcXdDeployButton.OnEffectEnd requires this death to complete synchronously
                    // (no yields), which only holds with teleportMurderer: false.
                    // showKillAnim false: vanilla's ShowKillAnimation is broken for killer ==
                    // victim (see Patches/SelfKillOverlayPatch.cs); skip it at the source too.
                    owner.RpcCustomMurder(owner, teleportMurderer: false, showKillAnim: false);
                }

                Info($"RC-XD car detonated: killed {filtered.Count} players (deployer died: {deployerDies})");
            }
        }

        DestroyLocally();
    }

    [MethodRpc((uint)SuperSquadRpc.DespawnRcXdCar, LocalHandling = RpcLocalHandling.Before)]
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
