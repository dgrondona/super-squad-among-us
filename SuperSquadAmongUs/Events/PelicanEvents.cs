using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.Events.Vanilla.Player;
using MiraAPI.Modifiers;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Roles.Neutral;
using TownOfUs.Events;
using TownOfUs.Modifiers;
using TownOfUs.Modules.Localization;
using TownOfUs.Utilities;

namespace SuperSquadAmongUs.Events;

/// <summary>
/// Resolves the Pelican's stomach contents. A meeting digests everyone currently devoured (they die,
/// no bodies), and a Pelican killed mid-round releases everyone alive from its stomach at the spot it died.
/// </summary>
public static class PelicanEvents
{
    /// <summary>
    /// Meeting called: everyone in a Pelican's stomach dies. Runs deterministically on every client
    /// (devoured state arrived via synced modifiers), before vote areas are built so the digested
    /// players show up as dead in the meeting.
    /// </summary>
    [RegisterEvent]
    public static void StartMeetingEventHandler(StartMeetingEvent @event)
    {
        foreach (var devoured in ModifierUtils.GetActiveModifiers<DevouredModifier>().ToList())
        {
            var target = devoured.Player;
            var pelican = devoured.Pelican;
            devoured.ModifierComponent?.RemoveModifier(devoured);

            if (target == null || target.HasDied())
            {
                continue;
            }

            DeathHandlerModifier.UpdateDeathHandlerImmediate(
                target,
                TouLocale.Get("SuperSquadDiedToPelican", "Digested"),
                DeathEventHandlers.CurrentRound,
                DeathHandlerOverride.SetFalse,
                TouLocale.GetParsed("DiedByStringBasic").Replace("<player>", pelican != null ? pelican.Data.PlayerName : "Pelican"),
                lockInfo: DeathHandlerOverride.SetTrue);

            // Exiled() kills without leaving a body - the victim was digested, there's nothing to find.
            target.Exiled();
        }
    }

    /// <summary>
    /// Pelican disconnected: release its stomach in place. Without this, devoured players would stay
    /// pinned and hidden forever - the death handler below never fires for a disconnect.
    /// </summary>
    [RegisterEvent]
    public static void PlayerLeaveEventHandler(PlayerLeaveEvent @event)
    {
        var player = @event.ClientData.Character;
        if (player == null || player.Data?.Role is not PelicanRole)
        {
            return;
        }

        foreach (var devoured in ModifierUtils.GetActiveModifiers<DevouredModifier>().ToList())
        {
            if (devoured.Pelican != null && devoured.Pelican.PlayerId == player.PlayerId)
            {
                devoured.ModifierComponent?.RemoveModifier(devoured);
            }
        }
    }

    /// <summary>
    /// Pelican killed mid-round: everyone in its stomach is released alive where it died. Each client
    /// removes the modifier locally (state is already synced); only the released player's own client
    /// performs the position snap, since movement is client-authoritative.
    /// </summary>
    [RegisterEvent]
    public static void PlayerDeathEventHandler(PlayerDeathEvent @event)
    {
        if (@event.Player == null || @event.Player.Data?.Role is not PelicanRole)
        {
            return;
        }

        var deathPosition = @event.Player.GetTruePosition();

        foreach (var devoured in ModifierUtils.GetActiveModifiers<DevouredModifier>().ToList())
        {
            if (devoured.Pelican == null || devoured.Pelican.PlayerId != @event.Player.PlayerId)
            {
                continue;
            }

            var target = devoured.Player;
            devoured.ModifierComponent?.RemoveModifier(devoured);

            if (target != null && !target.HasDied() && target.AmOwner)
            {
                target.NetTransform.RpcSnapTo(deathPosition);
            }
        }
    }
}
