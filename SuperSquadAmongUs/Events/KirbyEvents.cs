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
/// Resolves Kirby's swallowed players - a near-literal copy of <see cref="PelicanEvents"/>. A meeting
/// digests everyone currently swallowed (they die, no bodies) and, right before the kill, Kirby
/// permanently inherits a curated set of portable abilities from each digested victim's role (see
/// <c>AbilityGrants.ApplyPortableGrant</c>). A Kirby killed mid-round releases everyone alive
/// from its stomach at the spot it died - digestion never ran, so nothing was inherited from them.
/// </summary>
public static class KirbyEvents
{
    /// <summary>
    /// Meeting called: everyone in Kirby's stomach is digested (dies, no body) and Kirby inherits
    /// their portable abilities. Runs deterministically on every client (swallowed state arrived via
    /// synced modifiers), before vote areas are built so digested players show up as dead in the
    /// meeting.
    /// </summary>
    [RegisterEvent]
    public static void StartMeetingEventHandler(StartMeetingEvent @event)
    {
        foreach (var swallowed in ModifierUtils.GetActiveModifiers<KirbySwallowedModifier>().ToList())
        {
            var target = swallowed.Player;
            var kirby = swallowed.Kirby;
            swallowed.ModifierComponent?.RemoveModifier(swallowed);

            if (target == null || target.HasDied())
            {
                continue;
            }

            // Ability inheritance already happened at swallow time (KirbySwallowedModifier.OnActivate) -
            // digestion only kills the swallowed player here.
            DeathHandlerModifier.UpdateDeathHandlerImmediate(
                target,
                TouLocale.Get("SuperSquadDiedToKirby", "Digested"),
                DeathEventHandlers.CurrentRound,
                DeathHandlerOverride.SetFalse,
                TouLocale.GetParsed("DiedByStringBasic").Replace("<player>", kirby != null ? kirby.Data.PlayerName : "Kirby"),
                lockInfo: DeathHandlerOverride.SetTrue);

            // Exiled() kills without leaving a body - the victim was digested, there's nothing to find.
            target.Exiled();
        }
    }

    /// <summary>
    /// Kirby disconnected: release its stomach in place. Without this, swallowed players would stay
    /// pinned and hidden forever - the death handler below never fires for a disconnect.
    /// </summary>
    [RegisterEvent]
    public static void PlayerLeaveEventHandler(PlayerLeaveEvent @event)
    {
        var player = @event.ClientData.Character;
        if (player == null || player.Data?.Role is not KirbyRole)
        {
            return;
        }

        foreach (var swallowed in ModifierUtils.GetActiveModifiers<KirbySwallowedModifier>().ToList())
        {
            if (swallowed.Kirby != null && swallowed.Kirby.PlayerId == player.PlayerId)
            {
                swallowed.ModifierComponent?.RemoveModifier(swallowed);
            }
        }
    }

    /// <summary>
    /// Kirby killed mid-round: everyone in its stomach is released alive where it died. Each client
    /// removes the modifier locally (state is already synced); only the released player's own client
    /// performs the position snap, since movement is client-authoritative.
    /// </summary>
    [RegisterEvent]
    public static void PlayerDeathEventHandler(PlayerDeathEvent @event)
    {
        if (@event.Player == null || @event.Player.Data?.Role is not KirbyRole)
        {
            return;
        }

        var deathPosition = @event.Player.GetTruePosition();

        foreach (var swallowed in ModifierUtils.GetActiveModifiers<KirbySwallowedModifier>().ToList())
        {
            if (swallowed.Kirby == null || swallowed.Kirby.PlayerId != @event.Player.PlayerId)
            {
                continue;
            }

            var target = swallowed.Player;
            swallowed.ModifierComponent?.RemoveModifier(swallowed);

            if (target != null && !target.HasDied() && target.AmOwner)
            {
                target.NetTransform.RpcSnapTo(deathPosition);
            }
        }
    }
}
