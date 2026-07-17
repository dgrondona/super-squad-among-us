using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using SuperSquadAmongUs.Options.Roles.Crewmate;
using TownOfUs.Utilities;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// The "hidden in Daddy Hagrid's cloak" state: all mechanics live in <see cref="CarriedModifier"/>
/// (hidden, frozen, pinned to the Hagrid, untargetable). Unlike the Pelican's stomach this is timed -
/// the player pops out alive at the Hagrid's position when the duration runs out - and a meeting
/// releases them alive instead of killing them. Hagrid death/disconnect releases are handled in
/// Events/DaddyHagridEvents.cs.
/// </summary>
public sealed class CloakHiddenModifier(PlayerControl hagrid) : CarriedModifier(hagrid)
{
    /// <inheritdoc />
    public override string ModifierName => "Hidden in Cloak";

    /// <inheritdoc />
    public override float Duration => OptionGroupSingleton<DaddyHagridOptions>.Instance.HideDuration;

    /// <inheritdoc />
    public override bool AutoStart => true;

    /// <inheritdoc />
    public override string GetDescription()
    {
        return "Daddy Hagrid has hidden you in his cloak!";
    }

    /// <summary>
    /// Meeting called: pop out alive - the crucial difference from the Pelican's stomach.
    /// </summary>
    public override void OnMeetingStart()
    {
        Player.RemoveModifier(this);
    }

    /// <inheritdoc />
    public override void OnDeactivate()
    {
        base.OnDeactivate();

        // One owner-side snap covers every release path (timer pop-out, Hagrid death, disconnect):
        // the per-tick pin already placed this client's copy at the carrier's last position, so
        // re-broadcasting it re-syncs the just-unpaused NetTransform for everyone. Skipped during
        // meetings - vanilla repositions everyone for the vote anyway.
        if (Player.AmOwner && !Player.HasDied() && !MeetingHud.Instance && !ExileController.Instance)
        {
            Player.NetTransform.RpcSnapTo(Player.GetTruePosition());
        }
    }
}
