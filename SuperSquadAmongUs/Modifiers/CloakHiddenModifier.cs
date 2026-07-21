using MiraAPI.Modifiers;
using TownOfUs.Utilities;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// The "hidden in Daddy Hagrid's cloak" state: all mechanics live in <see cref="CarriedModifier"/>
/// (hidden, frozen, pinned to the Hagrid, untargetable). Unlike the Pelican's stomach the player pops
/// out alive - a meeting releases them alive instead of killing them, and the timed release is driven by
/// <see cref="Buttons.Crewmate.DaddyHagridHideButton"/>'s own effect timer (which broadcasts the removal
/// once the hide duration elapses or the Hagrid releases early), NOT by a self-expiring
/// <c>TimedModifier</c> here - so this stays indefinite until the caster's button, a meeting, or the
/// Hagrid's death removes it (the button owns the duration and its countdown, the same authority the
/// Dumper's Store effect uses). Hagrid death/disconnect releases are handled in
/// Events/DaddyHagridEvents.cs.
/// </summary>
public sealed class CloakHiddenModifier(PlayerControl hagrid) : CarriedModifier(hagrid)
{
    /// <inheritdoc />
    public override string ModifierName => "Hidden in Cloak";

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
