using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Modifiers;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modules;
using SuperSquadAmongUs.Options.Roles.Impostor;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs;
using TownOfUs.Buttons;
using TownOfUs.Modifiers;
using TownOfUs.Modifiers.Neutral;
using TownOfUs.Modules;
using TownOfUs.Modules.Localization;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Impostor;

/// <summary>
/// RC-XD Deploy/Detonate button. Deploy spawns the car and freezes the player, handing movement and
/// camera control to the car for a limited window; a second press detonates it, killing players in a
/// radius. If the window expires, the car despawns harmlessly.
/// </summary>
public sealed class RcXdDeployButton : TownOfUsRoleButton<RcXdRole>
{
    // Distinguishes "player pressed Detonate" from "drive time expired" - both funnel through
    // OnEffectEnd().
    private bool detonateRequested;

    // Guards Detonate against the SAME physical press that deployed: one press can dispatch
    // twice (same-frame double dispatch, or key autorepeat - observed under Proton, where a
    // single F tap deployed, detonated, and the death then let the held key open the vanilla
    // ghost Haunt menu). Time-based so it also absorbs autorepeat bursts across frames.
    private const float DetonateArmDelay = 0.3f;
    private float deployTime = float.NegativeInfinity;

    // Whether the freeze + camera + light drive state is applied, so EndDrive is idempotent
    // (reachable from OnEffectEnd and the FixedUpdate cancel path).
    private bool driveLockActive;

    // Post-detonation camera hold: camera and light stay on an anchor at the blast site so the
    // driver sees the explosion before control snaps back. Ticked in FixedUpdate, not a coroutine,
    // so the restore can't be stranded by an exception.
    private const float CameraLingerDuration = 1.5f;
    private bool cameraLingerActive;
    private float cameraLingerRemaining;
    private GameObject? cameraLingerAnchor;

    public override string Name => TouLocale.GetParsed("SuperSquadRoleRcXdDeploy", "Deploy");
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => TownOfUsColors.Impostor;
    public override float Cooldown => Math.Clamp(OptionGroupSingleton<RcXdOptions>.Instance.DeployCooldown + MapCooldown, 5f, 120f);
    public override float EffectDuration => OptionGroupSingleton<RcXdOptions>.Instance.DriveTime;
    public override LoadableAsset<Sprite> Sprite => SuperSquadImpAssets.RcXdDeploySprite;

    public override bool IsEffectCancellable() => true;

    // MiraAPI only drives a button's FixedUpdate while Enabled(role) is true, and dying swaps
    // Data.Role to a ghost role - so stay enabled while any drive/linger state is pending, or the
    // death-cancel and linger cleanup would stop ticking the moment the blast kills the deployer,
    // stranding the camera on the anchor (docs/il2cpp-gotchas.md).
    public override bool Enabled(RoleBehaviour? role)
    {
        return base.Enabled(role) || EffectActive || driveLockActive || cameraLingerActive;
    }

    public override bool CanUse()
    {
        if (HudManager.Instance.Chat.IsOpenOrOpening || MeetingHud.Instance)
        {
            return false;
        }

        // While driving, the player is deliberately frozen (moveable = false), which fails the TOU
        // base CanUse()'s CanMove check - but the Detonate press must still register. Replicate only
        // the guards that still apply while frozen; hacked/disabled are re-checked by ClickHandler.
        if (EffectActive)
        {
            return !PlayerControl.LocalPlayer.HasDied() &&
                   !TimeLordRewindSystem.IsRewinding &&
                   !PlayerControl.LocalPlayer.GetModifiers<DisabledModifier>().Any(x => !x.CanUseAbilities);
        }

        return base.CanUse();
    }

    public override void ClickHandler()
    {
        // TownOfUsButton.ClickHandler fully replaces MiraAPI's and has no cancellable-effect branch
        // (docs/il2cpp-gotchas.md), so the Detonate press is handled here instead of via base.
        if (EffectActive)
        {
            if (!CanClick() || PlayerControl.LocalPlayer.HasModifier<GlitchHackedModifier>() ||
                PlayerControl.LocalPlayer.GetModifiers<DisabledModifier>().Any(x => !x.CanUseAbilities))
            {
                return;
            }

            if (Time.time - deployTime < DetonateArmDelay)
            {
                return;
            }

            detonateRequested = true;
            ResetCooldownAndOrEffect();
            return;
        }

        base.ClickHandler();
    }

    protected override void OnClick()
    {
        var player = PlayerControl.LocalPlayer;
        detonateRequested = false;
        deployTime = Time.time;

        var pos = player.transform.position;
        RcXdCar.RpcDeployCar(player, pos.x, pos.y);

        BeginDrive();
    }

    public override void OnEffectEnd()
    {
        // Restore state before sending RPCs - an exception after the restore is harmless, but one
        // before it can strand a stale freeze on this process-lifetime button singleton
        // (docs/il2cpp-gotchas.md).
        var shouldDetonate = detonateRequested && RcXdCar.ActiveCar != null;
        var carPosition = shouldDetonate ? RcXdCar.ActiveCar!.transform.position : Vector3.zero;
        detonateRequested = false;

        if (shouldDetonate)
        {
            // The murder coroutine has no yields with teleportMurderer false, so a blast that
            // catches the deployer kills them SYNCHRONOUSLY inside RpcDetonateCar below - the whole
            // death (ghost role swap included) completes before it returns. So when the deployer is
            // in the blast, restore fully while still alive and skip the linger instead of restoring
            // onto a ghost mid-death-teardown: the explosion is on top of them anyway, and it avoids
            // touching camera/light state during death teardown (docs/roles/rc-xd.md).
            var options = OptionGroupSingleton<RcXdOptions>.Instance;
            var radius = options.ExplosionRadius.Value * ShipStatus.Instance.MaxLightRadius;
            var deployerInBlast = options.CanKillImpostors &&
                Helpers.GetClosestPlayers(new Vector2(carPosition.x, carPosition.y), radius)
                    .Any(p => p.AmOwner);

            if (deployerInBlast)
            {
                EndDrive();
            }
            else
            {
                BeginCameraLinger(carPosition);
            }

            try
            {
                RcXdCar.RpcDetonateCar(PlayerControl.LocalPlayer, carPosition.x, carPosition.y);
            }
            catch (Exception e)
            {
                Error($"RC-XD: detonate RPC failed - destroying the car locally instead: {e}");
                RcXdCar.EnsureDestroyedLocally();
            }

            return;
        }

        EndDrive();

        try
        {
            RcXdCar.RpcDespawnCar(PlayerControl.LocalPlayer);
        }
        catch (Exception e)
        {
            Error($"RC-XD: despawn RPC failed on effect end - destroying the car locally instead: {e}");
            RcXdCar.EnsureDestroyedLocally();
        }
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        if (EffectActive && (playerControl.HasDied() || MeetingHud.Instance))
        {
            EffectActive = false;
            SetTimer(Cooldown);
            detonateRequested = false;
            EndDrive();

            try
            {
                RcXdCar.RpcDespawnCar(playerControl);
            }
            catch (Exception e)
            {
                Error($"RC-XD: despawn RPC failed on cancel - destroying the car locally instead: {e}");
                RcXdCar.EnsureDestroyedLocally();
            }
        }

        // The car can disappear without this button's involvement (its own meeting safety net, or
        // stale state from a previous game) - treat that as a fizzle.
        if (EffectActive && RcXdCar.ActiveCar == null)
        {
            EffectActive = false;
            SetTimer(Cooldown);
            detonateRequested = false;
            EndDrive();
        }

        // Finish early if a meeting starts or the player dies (their own blast can kill them - the
        // death flow then owns the camera).
        if (cameraLingerActive)
        {
            cameraLingerRemaining -= Time.fixedDeltaTime;
            if (cameraLingerRemaining <= 0f || playerControl.HasDied() || MeetingHud.Instance)
            {
                FinishCameraLinger();
            }
        }

        // A drive lock without an active effect is stale (docs/il2cpp-gotchas.md) unless the camera
        // linger is deliberately holding it.
        if (!EffectActive && driveLockActive && !cameraLingerActive)
        {
            Error("RC-XD: stale drive lock without an active effect - restoring player state");
            EndDrive();
        }

        // Self-heal each tick: vanilla animations flip moveable back on. Skip once dead - a ghost
        // must never be held immobile just because EndDrive hasn't run yet this tick.
        if (driveLockActive && !playerControl.HasDied())
        {
            playerControl.moveable = false;
        }
    }

    private void BeginDrive()
    {
        var player = PlayerControl.LocalPlayer;

        player.moveable = false;
        player.MyPhysics.ResetMoveState();
        player.MyPhysics.body.velocity = Vector2.zero;
        player.NetTransform.SetPaused(true);
        player.NetTransform.ClearPositionQueues();
        player.SetKinematic(true);

        // SetTarget takes a MonoBehaviour, not a Transform.
        HudManager.Instance.PlayerCam.SetTarget(RcXdCar.ActiveBehaviour);

        // Reparenting keeps world position, so the local offset must be zeroed or the light lags
        // behind. ShadowQuad is untouched - no wall vision while driving (design decision).
        player.lightSource.transform.parent = RcXdCar.ActiveCar!.transform;
        player.lightSource.transform.localPosition = Vector3.zero;
        player.lightSource.Initialize(player.Collider.offset / 2f);

        OverrideSprite(SuperSquadImpAssets.RcXdDetonateSprite.LoadAsset());
        OverrideName(TouLocale.GetParsed("SuperSquadRoleRcXdDetonate", "Detonate"));

        driveLockActive = true;
    }

    // Idempotent counterpart to BeginDrive; reachable from OnEffectEnd and the FixedUpdate cancel path.
    private void EndDrive()
    {
        if (!driveLockActive)
        {
            return;
        }

        driveLockActive = false;
        var player = PlayerControl.LocalPlayer;

        // Camera and light first - a stuck camera is the worst failure mode if anything below throws.
        HudManager.Instance.PlayerCam.SetTarget(player);

        if (player.lightSource != null)
        {
            player.lightSource.transform.parent = player.transform;
            player.lightSource.transform.localPosition = Vector3.zero;
            player.lightSource.Initialize(player.Collider.offset / 2f);
        }
        else
        {
            Error("RC-XD: player lightSource missing during drive restore");
        }

        // Undo the BeginDrive physics state even for a ghost - a kinematic body ignores velocity,
        // which would leave the ghost unable to fly.
        player.moveable = true;
        player.NetTransform.SetPaused(false);
        player.SetKinematic(false);
        player.NetTransform.Halt();

        OverrideSprite(SuperSquadImpAssets.RcXdDeploySprite.LoadAsset());
        OverrideName(TouLocale.GetParsed("SuperSquadRoleRcXdDeploy", "Deploy"));
    }

    // Parks the camera and player's light on an inert anchor at the blast site before the detonate
    // RPC destroys the car, so the driver watches the explosion. driveLockActive stays true until
    // FinishCameraLinger restores everything.
    private void BeginCameraLinger(Vector3 blastPosition)
    {
        var player = PlayerControl.LocalPlayer;

        // An uninitialized RcXdCarBehaviour is inert - it only exists because SetTarget needs a
        // MonoBehaviour, not a Transform.
        cameraLingerAnchor = new GameObject("SuperSquadRcXdCamAnchor");
        cameraLingerAnchor.transform.position = blastPosition;
        var anchorBehaviour = cameraLingerAnchor.AddComponent<RcXdCarBehaviour>();

        HudManager.Instance.PlayerCam.SetTarget(anchorBehaviour);

        if (player.lightSource != null)
        {
            player.lightSource.transform.parent = cameraLingerAnchor.transform;
            player.lightSource.transform.localPosition = Vector3.zero;
            player.lightSource.Initialize(player.Collider.offset / 2f);
        }

        cameraLingerActive = true;
        cameraLingerRemaining = CameraLingerDuration;
    }

    private void FinishCameraLinger()
    {
        cameraLingerActive = false;
        EndDrive();

        if (cameraLingerAnchor != null)
        {
            UnityEngine.Object.Destroy(cameraLingerAnchor);
        }

        cameraLingerAnchor = null;
    }
}
