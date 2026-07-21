using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Player;
using MiraAPI.Modifiers;
using SuperSquadAmongUs.Modifiers;

namespace SuperSquadAmongUs.Events;

/// <summary>
/// Releases a <see cref="CloakHiddenModifier"/> when whoever cast it can no longer hold it. Mirror of
/// PelicanEvents minus the meeting handler: a meeting pops hidden players out ALIVE, which
/// <see cref="CloakHiddenModifier"/> handles itself in OnMeetingStart. Positioning on release also lives
/// in the modifier's OnDeactivate (owner-side snap), so these handlers only remove modifiers.
/// <para/>
/// Keyed on the modifier's own <see cref="CarriedModifier.Carrier"/>, NOT on the carrier being a
/// DaddyHagrid - so the identical cloak granted via <see cref="Modules.AbilityGrants"/> (Gooper/Kirby's
/// Hide) is released correctly too when its caster dies or disconnects.
/// </summary>
public static class DaddyHagridEvents
{
    /// <summary>
    /// The cloak's caster disconnected: release everyone they hid, in place. Without this, hidden
    /// players would stay pinned and hidden until their timers ran out at a stale position.
    /// </summary>
    [RegisterEvent]
    public static void PlayerLeaveEventHandler(PlayerLeaveEvent @event)
    {
        var player = @event.ClientData.Character;
        if (player == null)
        {
            return;
        }

        ReleaseCloaksCastBy(player);
    }

    /// <summary>
    /// The cloak's caster was killed mid-round: everyone they hid is released alive where the caster
    /// died. Each client removes the modifier locally (state is already synced); the released player's
    /// own client snaps position in the modifier's OnDeactivate.
    /// </summary>
    [RegisterEvent]
    public static void PlayerDeathEventHandler(PlayerDeathEvent @event)
    {
        if (@event.Player == null)
        {
            return;
        }

        ReleaseCloaksCastBy(@event.Player);
    }

    private static void ReleaseCloaksCastBy(PlayerControl caster)
    {
        foreach (var hidden in ModifierUtils.GetActiveModifiers<CloakHiddenModifier>().ToList())
        {
            if (hidden.Carrier != null && hidden.Carrier.PlayerId == caster.PlayerId)
            {
                hidden.ModifierComponent?.RemoveModifier(hidden);
            }
        }
    }
}
