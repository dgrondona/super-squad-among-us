using MiraAPI.GameOptions;
using SuperSquadAmongUs.Options.Roles.Neutral;
using TownOfUs.Modifiers;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// The Gooper's 1st-goop reward: temporary immunity to being killed. Shape copied from TOU-Mira's
/// <c>GuardianAngelProtectModifier</c> (a timed <c>BaseShieldModifier</c> that auto-clears on
/// death/meeting). Universal blocking - not just the handful of TOU-Mira kill sources that explicitly
/// check <c>BaseShieldModifier</c> - comes from <see cref="Events.GooperEvents"/>'s own
/// <c>BeforeMurderEvent</c>/<c>MiraButtonClickEvent</c> pair, copied from
/// <c>SuperSquadAmongUs.Events.ElusiveEvents</c> but cancel-only (no teleport). See docs/roles/gooper.md.
/// </summary>
public sealed class GooperVestModifier : BaseShieldModifier
{
    /// <inheritdoc />
    public override string ModifierName => "Vested";

    /// <inheritdoc />
    public override float Duration => OptionGroupSingleton<GooperOptions>.Instance.VestDuration;

    /// <inheritdoc />
    public override bool AutoStart => true;

    /// <inheritdoc />
    public override string ShieldDescription => "You are wearing a protective vest!\nYou cannot be killed.";

    /// <inheritdoc />
    public override void OnMeetingStart()
    {
        ModifierComponent?.RemoveModifier(this);
    }

    /// <inheritdoc />
    public override void OnDeath(DeathReason reason)
    {
        ModifierComponent?.RemoveModifier(this);
    }
}
