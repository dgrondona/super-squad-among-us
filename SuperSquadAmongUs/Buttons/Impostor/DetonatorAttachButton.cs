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
/// Detonator's Attach/Detonate toggle: plant a bomb on a nearby living player, then - once the arm
/// delay elapses - detonate it at will (no auto-expiry otherwise). Only one bomb is tracked at a time
/// per Detonator. See <see cref="DetonatorBombModifier"/> and <see cref="SuperSquadDetonator.RpcDetonate"/>.
/// </summary>
public sealed class DetonatorAttachButton : TownOfUsRoleButton<DetonatorRole, PlayerControl>
{
    private static string AttachLabel => TouLocale.GetParsed("SuperSquadRoleDetonatorAttach", "Attach");
    private static string DetonateLabel => TouLocale.GetParsed("SuperSquadRoleDetonatorDetonate", "Detonate");

    // Only one bomb at a time (confirmed default) - the currently-bombed target, if any.
    private PlayerControl? activeBomb;

    // SecondaryAction, not Primary: Detonator keeps the vanilla Impostor kill button (on PrimaryAction),
    // so Attach/Detonate lives on Secondary like RC-XD's Deploy and the Sniper's Snipe.
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => TownOfUsColors.Impostor;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<DetonatorOptions>.Instance.AttachCooldown + MapCooldown, 5f, 120f);

    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.ImpostorPlaceholderButton;
    public override string Name => AttachLabel;

    public override PlayerControl? GetTarget()
    {
        return PlayerControl.LocalPlayer.GetClosestLivingPlayer(true, Distance, false,
            x => !x.HasModifier<DetonatorBombModifier>());
    }

    // Lights/enables the button. Attach phase: normal proximity+cooldown check (the cooldown only ever
    // runs after a detonation). Detonate phase (a bomb is out): lit only once the arm delay has elapsed,
    // with no cooldown involved - the Detonator can then detonate at any time.
    public override bool CanUse()
    {
        if (HudManager.Instance.Chat.IsOpenOrOpening || MeetingHud.Instance)
        {
            return false;
        }

        if (activeBomb != null)
        {
            if (!activeBomb.HasModifier<DetonatorBombModifier>())
            {
                // The bomb resolved itself (meeting/death) without a click here - fall through to
                // the plain Attach flow below.
                activeBomb = null;
            }
            else
            {
                var armDelay = OptionGroupSingleton<DetonatorOptions>.Instance.ArmDelay;
                var armed = Time.time - activeBomb.GetModifier<DetonatorBombModifier>()!.PlantedAt >= armDelay;

                return armed && !PlayerControl.LocalPlayer.HasDied() &&
                       !PlayerControl.LocalPlayer.GetModifiers<DisabledModifier>().Any(x => !x.CanUseAbilities);
            }
        }

        return base.CanUse() && Target != null;
    }

    // Timing (per design): Attach plants a bomb with NO cooldown; after the arm delay the Detonator can
    // detonate at any time; the re-attach cooldown starts only AFTER detonating. That's why neither the
    // attach nor the "waiting to detonate" window runs a cooldown.
    //
    // This also has to sidestep the targeted-button ClickHandler, which routes through
    // CustomActionButton<T>.CanClick() - that hard-requires a fresh nearby Target AND Timer<=0, so a
    // Detonate press (no valid target under the cursor) would never register. Detonation is handled
    // directly here, gated only by CanUse()'s arm-delay/can-act checks (its activeBomb branch).
    public override void ClickHandler()
    {
        if (activeBomb != null && activeBomb.HasModifier<DetonatorBombModifier>())
        {
            if (!CanUse())
            {
                Info("Detonator: detonate press ignored - bomb not armed yet, or a meeting/chat/disabled " +
                     "state blocks it");
                return;
            }

            Info("Detonator: detonating");
            SuperSquadDetonator.RpcDetonate(PlayerControl.LocalPlayer, activeBomb);
            activeBomb = null;
            OverrideName(AttachLabel);

            // Re-attach cooldown begins here, after the detonation - the attach itself never started one.
            SetTimer(Cooldown);
            return;
        }

        // Attach phase: same gating as the base targeted ClickHandler (CanClick + hacked/disabled), but
        // deliberately WITHOUT the Timer = Cooldown it would set - attaching is free; only detonating
        // starts the cooldown.
        if (!CanClick() || PlayerControl.LocalPlayer.HasModifier<GlitchHackedModifier>() ||
            PlayerControl.LocalPlayer.GetModifiers<DisabledModifier>().Any(x => !x.CanUseAbilities))
        {
            return;
        }

        OnClick();
    }

    protected override void OnClick()
    {
        if (Target == null)
        {
            return;
        }

        Target.RpcAddModifier<DetonatorBombModifier>(PlayerControl.LocalPlayer);
        activeBomb = Target;
        OverrideName(DetonateLabel);
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        if (activeBomb != null && !activeBomb.HasModifier<DetonatorBombModifier>())
        {
            // Resolved by its own lifecycle (meeting start or the target's death) - relabel.
            activeBomb = null;
        }

        OverrideName(activeBomb != null ? DetonateLabel : AttachLabel);
    }
}
