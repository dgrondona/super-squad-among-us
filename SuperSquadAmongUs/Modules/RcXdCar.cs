using System.Collections;
using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using Reactor.Utilities;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Options.Roles.Impostor;
using TownOfUs.Assets;
using TownOfUs.Modifiers;
using TownOfUs.Networking;
using TownOfUs.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// The RC-XD's car, spawned on every client via RPC. Only the deployer's client simulates physics and
/// broadcasts throttled position updates; remote clients interpolate toward the latest received position.
/// </summary>
public static class RcXdCar
{
    /// <summary>
    /// The active car GameObject, if one is deployed (null otherwise).
    /// </summary>
    internal static GameObject? ActiveCar { get; private set; }

    /// <summary>
    /// The active car's behaviour component, if one is deployed (null otherwise).
    /// </summary>
    internal static RcXdCarBehaviour? ActiveBehaviour { get; private set; }

    /// <summary>
    /// Spawns the RC-XD car on every client at the deployer's current position.
    /// </summary>
    /// <param name="owner">The impostor deploying the car.</param>
    /// <param name="x">World x position of the car.</param>
    /// <param name="y">World y position of the car.</param>
    [MethodRpc((uint)SuperSquadRpc.DeployRcXdCar, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcDeployCar(PlayerControl owner, float x, float y)
    {
        DestroyLocally();

        var carObject = new GameObject("SuperSquadRcXdCar");
        carObject.transform.position = new Vector3(x, y, y / 1000f);

        var renderer = carObject.AddComponent<SpriteRenderer>();
        renderer.sprite = SuperSquadAssets.RcXdCarSprite.LoadAsset();

        var behaviour = carObject.AddComponent<RcXdCarBehaviour>();
        behaviour.Initialize(owner);

        ActiveCar = carObject;
        ActiveBehaviour = behaviour;
    }

    /// <summary>
    /// Updates the car's target position on remote clients. The deployer's client ignores this (already
    /// authoritative via physics simulation).
    /// </summary>
    /// <param name="owner">The impostor driving the car.</param>
    /// <param name="x">The new target x position.</param>
    /// <param name="y">The new target y position.</param>
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

    /// <summary>
    /// Detonates the car at the given position, playing explosion feedback on every client. The kills
    /// are computed and sent on the deployer's client only - RpcSpecialMultiMurder is itself an RPC and
    /// must go out exactly once.
    /// </summary>
    /// <param name="owner">The impostor who deployed the car.</param>
    /// <param name="x">The explosion center x position.</param>
    /// <param name="y">The explosion center y position.</param>
    [MethodRpc((uint)SuperSquadRpc.DetonateRcXdCar, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcDetonateCar(PlayerControl owner, float x, float y)
    {
        var options = OptionGroupSingleton<RcXdOptions>.Instance;
        var explosionPos = new Vector3(x, y, y / 1000f);

        TouAudio.PlaySound(TouAudio.ArsoIgniteSound);

        var sphere = MiscUtils.CreateSpherePrimitive(explosionPos, options.ExplosionRadius.Value);
        sphere.GetComponent<MeshRenderer>().material = AuAvengersAnims.IgniteMaterial.LoadAsset();
        Coroutines.Start(DestroyAfterDelay(sphere, 0.4f));

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
                owner.RpcSpecialMultiMurder(filtered, true, teleportMurderer: false, playKillSound: true,
                    causeOfDeath: "SuperSquadRcXd");
                Info($"RC-XD car detonated: killed {filtered.Count} players");
            }
        }

        DestroyLocally();
    }

    /// <summary>
    /// Despawns the car without detonation (fizzle path when the drive time expires).
    /// </summary>
    /// <param name="owner">The impostor who deployed the car.</param>
    [MethodRpc((uint)SuperSquadRpc.DespawnRcXdCar, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcDespawnCar(PlayerControl owner)
    {
        DestroyLocally();
    }

    /// <summary>
    /// Destroys the car GameObject and clears the statics on the calling client only. Shared by all
    /// RPC handlers to avoid nested RPC broadcasts.
    /// </summary>
    private static void DestroyLocally()
    {
        if (ActiveCar != null)
        {
            // The driver's client parents the player's lightSource to the car and points the
            // follower camera at it; both must be rescued BEFORE the car is destroyed, no matter
            // which path is destroying it (detonate, fizzle, meeting safety net, stale-deploy
            // cleanup) - destroying the light blacks out the screen for the rest of the game.
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
    /// Wrapper for DestroyLocally, exposed for safety-net cleanup from the behaviour when a meeting
    /// starts mid-drive.
    /// </summary>
    internal static void EnsureDestroyedLocally()
    {
        DestroyLocally();
    }

    private static IEnumerator DestroyAfterDelay(GameObject sphere, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (sphere != null)
        {
            Object.Destroy(sphere);
        }
    }
}
