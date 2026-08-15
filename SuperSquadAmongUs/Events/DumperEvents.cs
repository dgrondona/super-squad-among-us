using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Player;
using MiraAPI.Modifiers;
using SuperSquadAmongUs.Modifiers;

namespace SuperSquadAmongUs.Events;

/// <summary>
/// Releases a <see cref="DumperCarryModifier"/> when the carrying Dumper disconnects. A
/// meeting or the Dumper's own death already drops the body via the modifier's own
/// OnMeetingStart/OnDeath, but a disconnect skips both - without this handler the body's
/// renderers/collider stay hidden and unreportable forever, which can stall the round.
/// </summary>
public static class DumperEvents
{
    [RegisterEvent]
    public static void PlayerLeaveEventHandler(PlayerLeaveEvent @event)
    {
        var player = @event.ClientData.Character;
        if (player == null)
        {
            return;
        }

        foreach (var carry in ModifierUtils.GetActiveModifiers<DumperCarryModifier>().ToList())
        {
            if (carry.Player != null && carry.Player.PlayerId == player.PlayerId)
            {
                carry.ModifierComponent?.RemoveModifier(carry);
            }
        }
    }
}
