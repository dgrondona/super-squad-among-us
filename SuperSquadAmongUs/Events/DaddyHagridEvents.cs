using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Player;
using MiraAPI.Modifiers;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Roles.Crewmate;

namespace SuperSquadAmongUs.Events;

/// <summary>
/// Releases Daddy Hagrid's cloak when he can no longer hold it. Mirror of PelicanEvents minus the
/// meeting handler: a meeting pops hidden players out ALIVE, which <see cref="CloakHiddenModifier"/>
/// handles itself in OnMeetingStart. Positioning on release also lives in the modifier's
/// OnDeactivate (owner-side snap), so these handlers only remove modifiers.
/// </summary>
public static class DaddyHagridEvents
{
    /// <summary>
    /// Hagrid disconnected: release everyone hidden in his cloak in place. Without this, hidden
    /// players would stay pinned and hidden until their timers ran out at a stale position.
    /// </summary>
    [RegisterEvent]
    public static void PlayerLeaveEventHandler(PlayerLeaveEvent @event)
    {
        var player = @event.ClientData.Character;
        if (player == null || player.Data?.Role is not DaddyHagridRole)
        {
            return;
        }

        foreach (var hidden in ModifierUtils.GetActiveModifiers<CloakHiddenModifier>().ToList())
        {
            if (hidden.Carrier != null && hidden.Carrier.PlayerId == player.PlayerId)
            {
                hidden.ModifierComponent?.RemoveModifier(hidden);
            }
        }
    }

    /// <summary>
    /// Hagrid killed mid-round: everyone in his cloak is released alive where he died. Each client
    /// removes the modifier locally (state is already synced); the released player's own client
    /// snaps position in the modifier's OnDeactivate.
    /// </summary>
    [RegisterEvent]
    public static void PlayerDeathEventHandler(PlayerDeathEvent @event)
    {
        if (@event.Player == null || @event.Player.Data?.Role is not DaddyHagridRole)
        {
            return;
        }

        foreach (var hidden in ModifierUtils.GetActiveModifiers<CloakHiddenModifier>().ToList())
        {
            if (hidden.Carrier != null && hidden.Carrier.PlayerId == @event.Player.PlayerId)
            {
                hidden.ModifierComponent?.RemoveModifier(hidden);
            }
        }
    }
}
