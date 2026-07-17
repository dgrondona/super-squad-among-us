using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Modifiers.Types;
using SuperSquadAmongUs.Options.Roles.Crewmate;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// The Elusive's active shield (VeteranAlertModifier shape): while this is on, anyone who interacts
/// with the Elusive has the interaction cancelled and is teleported to a random spot on the map
/// instead - see Events/ElusiveEvents.cs. Deliberately no visual: a visible shield would tell
/// attackers not to bother, defeating the bluff.
/// </summary>
public sealed class ElusiveShieldModifier : TimedModifier
{
    /// <inheritdoc />
    public override string ModifierName => "Shielded";

    /// <inheritdoc />
    public override float Duration => OptionGroupSingleton<ElusiveOptions>.Instance.ShieldDuration;

    /// <inheritdoc />
    public override bool HideOnUi => false;

    /// <inheritdoc />
    public override string GetDescription()
    {
        return "Anyone who interacts with you is whisked away!";
    }

    /// <summary>
    /// Ends the shield early when a meeting is called; it does not resume afterwards.
    /// </summary>
    public override void OnMeetingStart()
    {
        Player.RemoveModifier(this);
    }

    /// <inheritdoc />
    public override void OnDeath(DeathReason reason)
    {
        Player.RemoveModifier(this);
    }
}
