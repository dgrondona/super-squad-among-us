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
/// Dumper's Store/Dump toggle: pick up the nearest unreported body (hidden while held - see
/// <see cref="DumperCarryModifier"/>), or dump it early while carrying. The store runs as a cancellable
/// button EFFECT so the player sees a fill-up countdown of the remaining store time and can dump early
/// mid-countdown (the RC-XD pattern). It auto-dumps when the effect times out; a meeting or the Dumper's
/// death drop it via the modifier's own lifecycle, which this button then detects to end the effect.
/// </summary>
public sealed class DumperCarryButton : SuperSquadRoleButton<DumperRole, DeadBody>
{
    // SecondaryAction, not Primary: Dumper keeps the vanilla Impostor kill button (on PrimaryAction), so
    // Store lives on Secondary like RC-XD's Deploy and the Undertaker's Drag.
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => TownOfUsColors.Impostor;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<DumperOptions>.Instance.CarryCooldown + MapCooldown, 5f, 120f);

    // The store duration IS the button's effect duration - that's what drives the visible fill-up
    // countdown. Cancellable so a mid-store press dumps early instead of being ignored.
    public override float EffectDuration => OptionGroupSingleton<DumperOptions>.Instance.CarryDuration;
    public override bool IsEffectCancellable() => true;

    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.ImpostorPlaceholderButton;
    public override string Name => StoreLabel;

    private static string StoreLabel => TouLocale.GetParsed("SuperSquadRoleDumperCarry", "Store");
    private static string DumpLabel => TouLocale.GetParsed("SuperSquadRoleDumperDrop", "Dump");

    // When the current body was stored (local Time.time), only for the sync-settle grace below - a
    // freshly-added modifier isn't visible via HasModifier for a tick or two.
    private float storedTime = float.NegativeInfinity;
    private const float SyncSettleTime = 2f;

    // Guards one physical press dispatching twice (keybind + click, or Proton key autorepeat) storing
    // and instantly dumping the same body.
    private const float ToggleDebounce = 0.3f;
    private float lastToggleTime = float.NegativeInfinity;

    // Keep this button ticking while its effect is live even if the role momentarily stops matching
    // (e.g. the Dumper dies mid-store and swaps to a ghost role), so FixedUpdate can still end the
    // effect and clear the stale state - same reason RC-XD keeps Enabled true during its drive.
    public override bool Enabled(RoleBehaviour? role)
    {
        return base.Enabled(role) || EffectActive;
    }

    public override DeadBody? GetTarget()
    {
        return PlayerControl.LocalPlayer.GetNearestDeadBody(Distance);
    }

    public override bool IsTargetValid(DeadBody? target)
    {
        return target != null && !target.Reported;
    }

    public override bool CanUse()
    {
        if (HudManager.Instance.Chat.IsOpenOrOpening || MeetingHud.Instance)
        {
            return false;
        }

        // While the effect is active (carrying), the button stays lit so it can be pressed to dump early.
        if (EffectActive)
        {
            return !PlayerControl.LocalPlayer.HasDied() &&
                   !PlayerControl.LocalPlayer.HasModifier<GlitchHackedModifier>() &&
                   !PlayerControl.LocalPlayer.GetModifiers<DisabledModifier>().Any(x => !x.CanUseAbilities);
        }

        return base.CanUse() && Timer <= 0f && Target != null;
    }

    public override void ClickHandler()
    {
        if (Time.time - lastToggleTime < ToggleDebounce)
        {
            return;
        }

        // Dump early: cancel the effect, which ends it via OnEffectEnd and starts the cooldown.
        if (EffectActive)
        {
            if (!CanUse())
            {
                return;
            }

            ResetCooldownAndOrEffect();
            lastToggleTime = Time.time;
            return;
        }

        // Store: the base targeted ClickHandler owns the CanClick/hacked/disabled gates, calls OnClick,
        // then sets EffectActive + Timer = EffectDuration (starting the countdown).
        base.ClickHandler();
        if (EffectActive)
        {
            lastToggleTime = Time.time;
        }
    }

    // Store only - the dump path runs through OnEffectEnd, never here.
    protected override void OnClick()
    {
        if (Target == null)
        {
            return;
        }

        PlayerControl.LocalPlayer.RpcAddModifier<DumperCarryModifier>(Target.ParentId);
        storedTime = Time.time;
        OverrideName(DumpLabel);
    }

    // The terminal dump - reached both when the store duration times out and when a mid-store press
    // cancels the effect. Idempotent, so it's safe when a meeting/death already removed the modifier.
    public override void OnEffectEnd()
    {
        if (PlayerControl.LocalPlayer.HasModifier<DumperCarryModifier>())
        {
            PlayerControl.LocalPlayer.RpcRemoveModifier<DumperCarryModifier>();
        }

        OverrideName(StoreLabel);
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        // If the body was dropped out from under the effect (the modifier's own OnMeetingStart/OnDeath
        // removed it) while our effect is still running, end the effect so the countdown stops and the
        // cooldown starts. Gated by the sync-settle grace so the brief post-store window where the
        // just-added modifier isn't visible via HasModifier yet doesn't trip it.
        if (EffectActive && Time.time - storedTime > SyncSettleTime &&
            !playerControl.HasModifier<DumperCarryModifier>())
        {
            ResetCooldownAndOrEffect();
        }

        OverrideName(EffectActive ? DumpLabel : StoreLabel);
    }
}
