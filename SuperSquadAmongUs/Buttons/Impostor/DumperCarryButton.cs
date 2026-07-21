using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Modifiers;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Options.Roles.Impostor;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs;
using TownOfUs.Buttons;
using TownOfUs.Modifiers;
using TownOfUs.Modifiers.Neutral;
using TownOfUs.Modules.Localization;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Impostor;

/// <summary>
/// Dumper's Carry/Drop toggle: pick up the nearest unreported body (hidden while held - see
/// <see cref="DumperCarryModifier"/>), or, while already carrying one, drop it early at the Dumper's
/// current position. Also auto-drops on its own after the configured duration, a meeting, or the
/// Dumper's death - all handled by the modifier's own lifecycle, not this button.
/// </summary>
public sealed class DumperCarryButton : SuperSquadRoleButton<DumperRole, DeadBody>
{
    // SecondaryAction, not Primary: Dumper keeps the vanilla Impostor kill button (on PrimaryAction), so
    // Carry lives on Secondary like RC-XD's Deploy and the Undertaker's Drag.
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => TownOfUsColors.Impostor;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<DumperOptions>.Instance.CarryCooldown + MapCooldown, 5f, 120f);

    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.ImpostorPlaceholderButton;

    public override string Name => TouLocale.GetParsed("SuperSquadRoleDumperCarry", "Carry");

    // Name is only read once at button creation; label swaps need an explicit OverrideName call (same
    // reason RcXdDeployButton swaps Deploy/Detonate this way instead of a computed Name getter).
    private static string CarryLabel => TouLocale.GetParsed("SuperSquadRoleDumperCarry", "Carry");
    private static string DropLabel => TouLocale.GetParsed("SuperSquadRoleDumperDrop", "Drop");

    // The carry cooldown is a *drop* cooldown - it starts when the body is dropped, not when it's picked
    // up (so carry duration and cooldown are independent). Tracked here to detect the pickup -> drop
    // transition each tick, including auto-drops the button never sees a click for.
    private bool wasCarrying;

    // Guards against one physical press dispatching twice (keybind + click, or key autorepeat under
    // Proton - see RcXdDeployButton) picking up and instantly dropping the same body.
    private const float ToggleDebounce = 0.3f;
    private float lastToggleTime = float.NegativeInfinity;

    public override DeadBody? GetTarget()
    {
        return PlayerControl.LocalPlayer.GetNearestDeadBody(Distance);
    }

    public override bool IsTargetValid(DeadBody? target)
    {
        return target != null && !target.Reported;
    }

    // While carrying, dropping early must not be gated by the pickup's own cooldown (TownOfUsButton's
    // base CanUse() checks Timer<=0) - same "bypass base while a special state is active" pattern
    // RcXdDeployButton/SniperSnipeButton use.
    public override bool CanUse()
    {
        if (HudManager.Instance.Chat.IsOpenOrOpening || MeetingHud.Instance)
        {
            return false;
        }

        if (PlayerControl.LocalPlayer.HasModifier<DumperCarryModifier>())
        {
            return !PlayerControl.LocalPlayer.HasDied() &&
                   !PlayerControl.LocalPlayer.HasModifier<GlitchHackedModifier>() &&
                   !PlayerControl.LocalPlayer.GetModifiers<DisabledModifier>().Any(x => !x.CanUseAbilities);
        }

        return base.CanUse() && Target != null;
    }

    // The targeted-button ClickHandler routes through CustomActionButton<T>.CanClick(), which hard-requires
    // a fresh nearby Target AND Timer<=0. Neither holds while carrying: the body is hidden and teleported
    // out from under the player - so an early drop would never register. Route the drop straight through
    // CanUse() (which owns the carry-state guards) instead. Neither phase starts the cooldown here: the
    // pickup mustn't (carrying is free until the duration/drop), and the drop's cooldown is started by the
    // FixedUpdate transition below so early drops and auto-drops behave identically.
    public override void ClickHandler()
    {
        if (Time.time - lastToggleTime < ToggleDebounce)
        {
            return;
        }

        if (PlayerControl.LocalPlayer.HasModifier<DumperCarryModifier>())
        {
            if (!CanUse())
            {
                return;
            }

            OnClick();
            lastToggleTime = Time.time;
            return;
        }

        // Pickup: same gating as the base targeted ClickHandler (CanClick + hacked/disabled), minus the
        // Timer = Cooldown it would set - picking up must not put the button on cooldown.
        if (!CanClick() || PlayerControl.LocalPlayer.HasModifier<GlitchHackedModifier>() ||
            PlayerControl.LocalPlayer.GetModifiers<DisabledModifier>().Any(x => !x.CanUseAbilities))
        {
            return;
        }

        OnClick();
        lastToggleTime = Time.time;
    }

    protected override void OnClick()
    {
        if (PlayerControl.LocalPlayer.HasModifier<DumperCarryModifier>())
        {
            PlayerControl.LocalPlayer.RpcRemoveModifier<DumperCarryModifier>();
            OverrideName(CarryLabel);
            return;
        }

        if (Target == null)
        {
            return;
        }

        PlayerControl.LocalPlayer.RpcAddModifier<DumperCarryModifier>(Target.ParentId);
        OverrideName(DropLabel);
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        var carrying = playerControl.HasModifier<DumperCarryModifier>();

        // Start the drop cooldown the moment the carry ends by ANY path - early drop, duration expiry,
        // meeting, or death - so the cooldown is always measured from the drop, never the pickup.
        if (wasCarrying && !carrying)
        {
            SetTimer(Cooldown);
        }

        wasCarrying = carrying;

        // Name is a fixed expression body (like every other button's), so it can't reflect what
        // OverrideName last set - re-assert the correct label every tick. Cheap, and self-heals the
        // label if the carry ended without a click here.
        OverrideName(carrying ? DropLabel : CarryLabel);
    }
}
