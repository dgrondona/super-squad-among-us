using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Modifiers;
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
/// RC-XD Deploy/Detonate button. A two-phase button: Deploy spawns the car, freezes the player in place,
/// and hands movement/camera control to the car for a limited window (default 8 s). During the window,
/// pressing the button again detonates the car, killing players in a radius. If the window expires without
/// detonation, the car despawns harmlessly (Sniper-style expiring window).
/// </summary>
public sealed class RcXdDeployButton : TownOfUsRoleButton<RcXdRole>
{
    // Whether a detonate press has been requested during the active drive window. Signals to
    // OnEffectEnd() that we should explode the car rather than fizzle it.
    private bool detonateRequested;

    // Whether the freeze + camera + lightSource drive state is currently applied, so EndDrive
    // is idempotent (it can be reached from OnEffectEnd and the FixedUpdate cancel path).
    private bool driveLockActive;

    public override string Name => TouLocale.GetParsed("SuperSquadRoleRcXdDeploy", "Deploy");
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => TownOfUsColors.Impostor;
    public override float Cooldown => Math.Clamp(OptionGroupSingleton<RcXdOptions>.Instance.DeployCooldown + MapCooldown, 5f, 120f);
    public override float EffectDuration => OptionGroupSingleton<RcXdOptions>.Instance.DriveTime;
    public override LoadableAsset<Sprite> Sprite => SuperSquadImpAssets.RcXdDeploySprite;

    public override bool IsEffectCancellable() => true;

    public override bool CanUse()
    {
        if (HudManager.Instance.Chat.IsOpenOrOpening || MeetingHud.Instance)
        {
            return false;
        }

        // While driving, the player is deliberately frozen (moveable = false), which fails the TOU
        // base CanUse()'s CanMove check - but the Detonate press must still register (MiraAPI's
        // CanClick is `(EffectActive ? IsEffectCancellable() : Timer <= 0) && CanUse()`, so CanUse
        // must stay true during the effect). Replicate the base guards that still apply while
        // frozen; hacked/disabled are re-checked by TOU's own ClickHandler on top.
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
        // TOU's TownOfUsButton.ClickHandler fully replaces MiraAPI's and has NO cancellable-effect
        // branch: with the effect active, deferring to base would go straight to OnClick() -
        // redeploying the car at the player's feet and restarting the countdown (2026-07-17
        // playtest bug). Handle the Detonate press entirely here instead, replicating TOU's
        // hacked/disabled gating (docs/il2cpp-gotchas.md "Overriding TownOfUsButton.ClickHandler
        // drops the hacked/disabled gating").
        if (EffectActive)
        {
            if (!CanClick() || PlayerControl.LocalPlayer.HasModifier<GlitchHackedModifier>() ||
                PlayerControl.LocalPlayer.GetModifiers<DisabledModifier>().Any(x => !x.CanUseAbilities))
            {
                return;
            }

            // ResetCooldownAndOrEffect starts the deploy cooldown and fires OnEffectEnd, which
            // detonates because the flag is set.
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

        var pos = player.transform.position;
        RcXdCar.RpcDeployCar(player, pos.x, pos.y);

        BeginDrive();
    }

    public override void OnEffectEnd()
    {
        // Restore the player FIRST, RPC second: the 2026-07-17 playtest showed the original order
        // (despawn RPC, then restore) can leave the light parented to the destroyed car (black
        // screen) and driveLockActive stuck true when anything in the RPC path throws - and the
        // stuck flag kept re-freezing the player in every later game, because button singletons
        // persist for the whole process (see docs/il2cpp-gotchas.md).
        var shouldDetonate = detonateRequested && RcXdCar.ActiveCar != null;
        var carPosition = shouldDetonate ? RcXdCar.ActiveCar!.transform.position : Vector3.zero;
        detonateRequested = false;
        EndDrive();

        try
        {
            if (shouldDetonate)
            {
                RcXdCar.RpcDetonateCar(PlayerControl.LocalPlayer, carPosition.x, carPosition.y);
            }
            else
            {
                RcXdCar.RpcDespawnCar(PlayerControl.LocalPlayer);
            }
        }
        catch (Exception e)
        {
            Error($"RC-XD: car RPC failed on effect end - destroying the car locally instead: {e}");
            RcXdCar.EnsureDestroyedLocally();
        }
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        // Cancel the drive window as a fizzle (no explosion) if the player dies or a meeting
        // starts mid-drive. Restore first, RPC second (see OnEffectEnd).
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

        // The car can disappear without this button's involvement (the car's own meeting safety
        // net, or stale state from a previous game). Treat "effect active but no car" as a fizzle.
        if (EffectActive && RcXdCar.ActiveCar == null)
        {
            EffectActive = false;
            SetTimer(Cooldown);
            detonateRequested = false;
            EndDrive();
        }

        // A drive lock without an active effect is always stale (an exception skipped the restore,
        // possibly in a previous game - button singletons persist for the whole process). Recover
        // instead of freezing the player forever.
        if (!EffectActive && driveLockActive)
        {
            Error("RC-XD: stale drive lock without an active effect - restoring player state");
            EndDrive();
        }

        // Self-heal the freeze state each tick: vanilla animations flip moveable back on. House
        // doctrine, same as Sniper.
        if (driveLockActive)
        {
            playerControl.moveable = false;
        }
    }

    /// <summary>
    /// Freeze the local player, reparent the camera to the car, and reparent the light to the car.
    /// Sets the button sprite/name to Detonate.
    /// </summary>
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

        // Reparent the light to the car, same pattern as MedSpiritObject, and zero its local
        // offset - reparenting keeps the world position, so without this the light would lag
        // behind wherever the player stood. Do NOT touch ShadowQuad (no wall-vision for RC-XD,
        // unlike Sniper).
        player.lightSource.transform.parent = RcXdCar.ActiveCar!.transform;
        player.lightSource.transform.localPosition = Vector3.zero;
        player.lightSource.Initialize(player.Collider.offset / 2f);

        OverrideSprite(SuperSquadImpAssets.RcXdDetonateSprite.LoadAsset());
        OverrideName(TouLocale.GetParsed("SuperSquadRoleRcXdDetonate", "Detonate"));

        driveLockActive = true;
    }

    /// <summary>
    /// Idempotent counterpart to BeginDrive; reachable from OnEffectEnd and the FixedUpdate cancel path.
    /// Restores the player's movement, camera, and light, and reverts the button sprite/name.
    /// </summary>
    private void EndDrive()
    {
        if (!driveLockActive)
        {
            return;
        }

        driveLockActive = false;
        var player = PlayerControl.LocalPlayer;

        player.moveable = true;
        player.NetTransform.SetPaused(false);
        player.SetKinematic(false);
        player.NetTransform.Halt();

        HudManager.Instance.PlayerCam.SetTarget(player);

        // Snap the light's local offset back onto the player - reparenting keeps the world
        // position, so without this it stays hovering wherever the car ended up and the player is
        // left in the dark. Null-guarded: if the light was somehow destroyed with the car, a log
        // beats a per-tick NullReferenceException.
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

        OverrideSprite(SuperSquadImpAssets.RcXdDeploySprite.LoadAsset());
        OverrideName(TouLocale.GetParsed("SuperSquadRoleRcXdDeploy", "Deploy"));
    }
}
