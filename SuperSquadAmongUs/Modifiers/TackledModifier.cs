using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using SuperSquadAmongUs.Options.Modifiers;
using TownOfUs.Modifiers;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// The stun applied by a slide tackle (see <see cref="Buttons.Modifiers.SlideTackleButton"/>): the
/// victim can't move, use abilities, or report for the configured duration, but CAN still be
/// interacted with (a downed player is easy prey). Synced via RpcAddModifier; the timer runs on every
/// client and the movement freeze is owner-side only, like <see cref="CarriedModifier"/>'s.
/// </summary>
public sealed class TackledModifier : DisabledModifier
{
    /// <inheritdoc />
    public override string ModifierName => "Tackled";

    /// <inheritdoc />
    public override float Duration => OptionGroupSingleton<SlideTackleOptions>.Instance.StunDuration;

    /// <inheritdoc />
    public override bool AutoStart => true;

    /// <inheritdoc />
    public override bool HideOnUi => false;

    /// <inheritdoc />
    public override string GetDescription() => "You've been slide tackled!";

    /// <inheritdoc />
    public override void OnActivate()
    {
        base.OnActivate();

        // Freeze movement the way the Devoured pin does, minus the NetTransform pause - the victim
        // stays where they are and keeps broadcasting that position; killing momentum is enough.
        if (Player.AmOwner)
        {
            Player.moveable = false;
            Player.MyPhysics.ResetMoveState();
        }
    }

    /// <inheritdoc />
    public override void FixedUpdate()
    {
        base.FixedUpdate();

        // Self-heal per house doctrine: vanilla kill/vent/ladder animations flip moveable back on.
        if (Player.AmOwner && Player.moveable)
        {
            Player.moveable = false;
        }
    }

    /// <inheritdoc />
    public override void OnDeactivate()
    {
        if (Player.AmOwner)
        {
            Player.moveable = true;
        }
    }

    /// <summary>
    /// Clears the stun when a meeting is called; nobody should sit frozen through the vote.
    /// </summary>
    public override void OnMeetingStart()
    {
        Player.RemoveModifier(this);
    }
}
