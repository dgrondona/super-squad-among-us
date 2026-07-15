using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modules;
using SuperSquadAmongUs.Options.Roles.Crewmate;
using SuperSquadAmongUs.Roles.Crewmate;
using TownOfUs.Buttons;
using TownOfUs.Modules.Localization;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Crewmate;

public sealed class ApparaterMapButton : TownOfUsRoleButton<ApparaterRole>
{
    // Fallback probe radius if the local player's collider is ever unavailable when checking
    // (shouldn't normally happen - the player always has a collider while alive).
    private const float FallbackProbeRadius = 0.2f;

    // A click doesn't land exactly where the reachability search's nearest-valid-point does: for a
    // click that's basically on open floor (or barely clipping a wall/obstacle edge), that gap is a
    // small precision nudge. For a click on a wall, or entirely outside the ship, the nearest reachable
    // point can be far away (however far it is back to actual floor). Treating "far" as "the click
    // wasn't actually on the map" gives us "clicking outside the play area does nothing" without
    // needing to hit-test the map's artwork directly.
    private const float MaxSnapDistance = 1.5f;

    public override string Name => TouLocale.GetParsed("SuperSquadRoleApparaterTeleport", "Apparate");
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;
    public override Color TextOutlineColor => SuperSquadColors.Apparater;
    public override float Cooldown => Math.Clamp(OptionGroupSingleton<ApparaterOptions>.Instance.TeleportCooldown + MapCooldown, 5f, 120f);

    // There's no real selection-time limit - the player picks a spot whenever they want - but
    // TownOfUsButton.FixedUpdateHandler (which this button inherits, not the base CustomActionButton
    // version) ends the effect the instant Timer goes negative, unconditionally: `else if (HasEffect &&
    // EffectActive)`, with no "EffectDuration > 0" guard the way the base class has. So EffectDuration
    // can't just be 0 - Timer would go negative on literally the next tick and force-close the map
    // before the player can click anything. Instead it's re-armed to this value every tick in
    // FixedUpdate below, so it never actually gets the chance to run out; the constant itself just needs
    // to comfortably clear one tick's Time.deltaTime.
    private const float TimerKeepAlive = 5f;

    public override float EffectDuration => TimerKeepAlive;
    public override int MaxUses => (int)OptionGroupSingleton<ApparaterOptions>.Instance.MaxUses;
    public override LoadableAsset<Sprite> Sprite => SuperSquadCrewAssets.ApparaterMapSprite;

    // The base ClickHandler decrements UsesLeft the moment the button is pressed, before OnClick
    // even runs - so opening the map always "spends" a use as far as the base class is concerned.
    // We only want a use spent on an actual teleport, so we track whether one happened and refund
    // the use (and skip the cooldown) in OnEffectEnd, which fires on both paths the effect can end:
    // an actual teleport, or the player closing the map without picking a spot.
    private bool teleported;

    // The frame the map was opened on, so the same physical press that triggered the ability button
    // can't also register as a map-target click on that very first frame. See HandleMapClick.
    private int openedFrame;

    protected override void OnClick()
    {
        teleported = false;
        openedFrame = Time.frameCount;
        BareMapVisuals.Open(Palette.Blue);
    }

    public override void OnEffectEnd()
    {
        base.OnEffectEnd();
        BareMapVisuals.Close();

        if (teleported)
        {
            // Cooldown already applies: ResetCooldownAndOrEffect (the only caller of OnEffectEnd for
            // this button) sets Timer = Cooldown before calling us.
            return;
        }

        // No teleport happened - refund the use ClickHandler spent on opening the map, and don't make
        // the player wait out a cooldown for an ability they never actually used.
        if (LimitedUses)
        {
            IncreaseUses();
        }

        Timer = 0f;
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        if (!EffectActive)
        {
            return;
        }

        // Re-arm (see TimerKeepAlive) and hide the countdown text TownOfUsButton.FixedUpdateHandler
        // just drew above us this same tick - the map should just stay open, not visibly tick down.
        Timer = TimerKeepAlive;
        if (Button)
        {
            Button!.cooldownTimerText.gameObject.SetActive(false);
        }

        if (!MapBehaviour.Instance || !MapBehaviour.Instance.gameObject.activeSelf)
        {
            // The player closed the map themselves (e.g. the in-game close button) without picking a spot.
            ResetCooldownAndOrEffect();
        }
    }

    /// <summary>
    /// Handles a left-click while the teleport map is open: finds the nearest reachable point to the
    /// click and snaps the player there. Called once per frame a left-click begins, from
    /// <see cref="Patches.ApparaterMapClickPatch"/> (a <c>HudManager.Update</c> postfix), so it's synced
    /// to the render frame. Detecting the click here rather than by polling the mouse in
    /// <see cref="FixedUpdate"/> is the fix for clicks being dropped: FixedUpdate doesn't run on every
    /// rendered frame, so a quick click held for less than one fixed timestep was never sampled.
    /// </summary>
    public void HandleMapClick()
    {
        if (!EffectActive || !MapBehaviour.Instance || !MapBehaviour.Instance.gameObject.activeSelf)
        {
            return;
        }

        // The press that opened the map (clicking the ability button) shouldn't also count as a map
        // target on that same frame.
        if (Time.frameCount == openedFrame)
        {
            return;
        }

        var playerControl = PlayerControl.LocalPlayer;
        var rawTarget = GetRawClickWorldPosition();
        var origin = playerControl.GetTruePosition();
        var probeRadius = GetPlayerProbeRadius();

        if (!WalkableRegionSolver.TryFindReachablePoint(origin, rawTarget, probeRadius, playerControl.Collider, out var target))
        {
            Info($"Apparater: no reachable point found near raw click {rawTarget}");
            return;
        }

        var snapDistance = Vector2.Distance(target, rawTarget);
        if (snapDistance > MaxSnapDistance)
        {
            Info($"Apparater: click at {rawTarget} was {snapDistance} from the nearest reachable point {target} " +
                 $"(> {MaxSnapDistance}); treating as outside the play area and ignoring");
            return;
        }

        Info($"Apparater: teleporting {origin} -> {target} (raw click {rawTarget}, snapDistance={snapDistance})");
        teleported = true;
        playerControl.NetTransform.RpcSnapTo(target);
        ResetCooldownAndOrEffect();
    }

    private static Vector2 GetRawClickWorldPosition()
    {
        // Mirrors the math TownOfUs uses to place vent/body icons on the minimap
        // (worldPos / ShipStatus.Instance.MapScale), just inverted.
        var mapRoot = MapBehaviour.Instance.HerePoint.transform.parent;
        var clickPoint = Camera.main!.ScreenToWorldPoint(Input.mousePosition);
        var localPoint = mapRoot.InverseTransformPoint(clickPoint);
        return (Vector2)(localPoint * ShipStatus.Instance.MapScale);
    }

    private static float GetPlayerProbeRadius()
    {
        var collider = PlayerControl.LocalPlayer.Collider;
        return collider ? Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y) : FallbackProbeRadius;
    }
}
