using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Modifiers;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Modules;
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
/// Detonator's Attach/Detonate toggle: plant a bomb on a nearby living player, wait out a visible arm
/// countdown, then detonate it at will (no auto-expiry otherwise). Only one bomb at a time. See
/// <see cref="DetonatorBombModifier"/> and <see cref="SuperSquadDetonator.RpcDetonate"/>.
/// </summary>
public sealed class DetonatorAttachButton : SuperSquadRoleButton<DetonatorRole, PlayerControl>
{
    private static string AttachLabel => TouLocale.GetParsed("SuperSquadRoleDetonatorAttach", "Attach");
    private static string DetonateLabel => TouLocale.GetParsed("SuperSquadRoleDetonatorDetonate", "Detonate");

    // The player currently bombed by THIS Detonator, or null - the LOCAL source of truth for the
    // Attach vs. Detonate phase. Deliberately NOT re-derived from HasModifier each frame: a freshly-sent
    // RpcAddModifier is only queued onto the target's ModifierComponent and isn't visible via
    // HasModifier until its next FixedUpdate, so trusting it would flash the label right after attaching.
    // Cleared only on reliable signals - see FixedUpdate.
    private PlayerControl? activeBomb;

    // Guards one physical press dispatching twice (keybind + click, or Proton key autorepeat).
    private const float ToggleDebounce = 0.3f;
    private float lastToggleTime = float.NegativeInfinity;

    // SecondaryAction, not Primary: Detonator keeps the vanilla Impostor kill button (on PrimaryAction),
    // so Attach/Detonate lives on Secondary like RC-XD's Deploy and the Sniper's Snipe.
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => TownOfUsColors.Impostor;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<DetonatorOptions>.Instance.AttachCooldown + MapCooldown, 5f, 120f);

    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.ImpostorPlaceholderButton;
    public override string Name => AttachLabel;

    // The arm delay is driven by the button's own Timer: attaching sets Timer = ArmDelay, so the button
    // shows a plain cooldown-style countdown (disabled) for those seconds - "show a 5s cooldown on
    // attach". The bomb is armed (detonatable) once that Timer elapses. Using Timer this way means the
    // arm gate never depends on the synced modifier having reached any client, and it gives the visible
    // countdown for free. ArmDelay's minimum (2s) is far longer than the modifier sync window, so
    // "Timer has elapsed" doubles as "the modifier has certainly synced" for the stale-clear below.
    private bool Armed => activeBomb != null && Timer <= 0f;

    public override PlayerControl? GetTarget()
    {
        return PlayerControl.LocalPlayer.GetClosestLivingPlayer(true, Distance, false,
            x => !x.HasModifier<DetonatorBombModifier>());
    }

    // Attach phase: normal proximity + cooldown check (the cooldown only ever runs after a detonation).
    // Detonate phase (a bomb is out): lit only once the arm countdown has finished.
    public override bool CanUse()
    {
        if (HudManager.Instance.Chat.IsOpenOrOpening || MeetingHud.Instance)
        {
            return false;
        }

        if (activeBomb != null)
        {
            return Armed && !PlayerControl.LocalPlayer.HasDied() &&
                   !PlayerControl.LocalPlayer.HasModifier<GlitchHackedModifier>() &&
                   !PlayerControl.LocalPlayer.GetModifiers<DisabledModifier>().Any(x => !x.CanUseAbilities);
        }

        return base.CanUse() && Timer <= 0f && Target != null;
    }

    // Attaching plants a bomb and starts the arm countdown (Timer = ArmDelay). Detonating happens any
    // time after that and starts the re-attach cooldown (Timer = AttachCooldown). The base targeted
    // click handler is bypassed because its can-click gate hard-requires both a fresh nearby target and
    // an elapsed timer, which a detonate press never has, so both phases run directly here instead.
    public override void ClickHandler()
    {
        if (Time.time - lastToggleTime < ToggleDebounce)
        {
            return;
        }

        if (activeBomb != null)
        {
            if (!CanUse())
            {
                Info("Detonator: detonate press ignored - still arming, or a meeting/chat/hacked/disabled " +
                     "state blocks it");
                return;
            }

            Info("Detonator: detonating");
            SuperSquadDetonator.RpcDetonate(PlayerControl.LocalPlayer, activeBomb);
            activeBomb = null;
            OverrideName(AttachLabel);

            // Re-attach cooldown begins here, after the detonation - the attach itself started only the
            // arm countdown, not this cooldown.
            SetTimer(Cooldown);
            lastToggleTime = Time.time;
            return;
        }

        // Attach phase: same gating as the base targeted ClickHandler (CanClick + hacked/disabled).
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
        if (Target == null)
        {
            return;
        }

        Target.RpcAddModifier<DetonatorBombModifier>(PlayerControl.LocalPlayer);
        activeBomb = Target;

        // Start the visible arm countdown - the bomb can't be detonated until it reaches zero. The label
        // stays "Attach" (greyed, counting down) while arming and flips to "Detonate" only once armed.
        SetTimer(OptionGroupSingleton<DetonatorOptions>.Instance.ArmDelay);
        OverrideName(AttachLabel);
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        // Clear the bomb on reliable signals only (see the activeBomb field comment): the target dying
        // or a meeting starting - both also remove the synced modifier - plus, once the arm countdown
        // has elapsed (so the modifier has certainly synced), the modifier genuinely being absent
        // (external removal, or stale state from a previous game). Never on a bare same-frame check
        // during the arm countdown, which would flash the label.
        if (activeBomb != null &&
            (activeBomb.HasDied() || MeetingHud.Instance ||
             (Timer <= 0f && !activeBomb.HasModifier<DetonatorBombModifier>())))
        {
            activeBomb = null;
        }

        // "Detonate" only once armed (bomb placed and countdown finished); "Attach" both while arming
        // (greyed, counting down) and while idle / on the post-detonate cooldown.
        OverrideName(Armed ? DetonateLabel : AttachLabel);
    }
}
