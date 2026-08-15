using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Networking;
using MiraAPI.Utilities;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Options.Roles.Impostor;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs.Modifiers;
using TownOfUs.Modifiers.Neutral;
using TownOfUs.Networking;
using TownOfUs.Utilities;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// The Detonator's detonate: kills the bombed target and anyone within a small radius of the target's
/// CURRENT position (not the position at attach time - the bomb travels with a live target, unlike
/// TOU-Mira's Bomber which plants a stationary charge). No-ops if a meeting has started, mirroring
/// TOU-Mira's <c>Bomb.CoDetonate</c>; <see cref="DetonatorBombModifier.OnMeetingStart"/> already
/// removes the bomb the instant a meeting starts, so this is a second line of defense against a
/// same-frame race, not the primary "bomb removed on meeting" mechanism.
/// </summary>
public static class SuperSquadDetonator
{
    /// <summary>
    /// Detonates the bomb on <paramref name="target"/>. Only the Detonator's own client computes and
    /// sends the kill (<c>RpcSpecialMultiMurder</c> is itself an RPC).
    /// </summary>
    /// <param name="source">The Detonator.</param>
    /// <param name="target">The bombed player.</param>
    [MethodRpc((uint)SuperSquadRpc.DetonatorDetonate, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcDetonate(PlayerControl source, PlayerControl target)
    {
        if (!AbilityGrants.SenderIsOrHolds<DetonatorRole>(source))
        {
            Error("RpcDetonate - Invalid Detonator");
            return;
        }

        // Holding the Detonator ability isn't the same as owning THIS bomb: the kit is borrowable via
        // AbilityGrants, so with multiple simultaneous bomb-holders (e.g. a Kirby that swallowed a
        // Detonator) a holder could otherwise detonate a bomb it didn't place. Require the sender to be
        // the specific player who placed this target's bomb.
        if (!target.TryGetModifier<DetonatorBombModifier>(out var bomb) || bomb.Detonator != source)
        {
            Error("RpcDetonate - Sender did not place this bomb");
            return;
        }

        if (!MeetingHud.Instance && !ExileController.Instance && source.AmOwner)
        {
            var options = OptionGroupSingleton<DetonatorOptions>.Instance;
            var pos = target.GetTruePosition();
            var radius = options.BlastRadius.Value * ShipStatus.Instance.MaxLightRadius;
            var victims = Helpers.GetClosestPlayers(pos, radius);

            var filtered = new List<PlayerControl>();
            foreach (var player in victims)
            {
                if (player == null || player.Data == null || player.Data.IsDead || player.Data.Disconnected || player.inVent)
                {
                    continue;
                }

                if (!options.CanKillImpostors && player.IsImpostorAligned())
                {
                    continue;
                }

                if (player.HasModifier<FirstDeadShield>() ||
                    player.GetModifiers<DisabledModifier>().Any(x => !x.CanBeInteractedWith))
                {
                    continue;
                }

                filtered.Add(player);
            }

            if (filtered.Count > 0)
            {
                // Explicit OutsideMeeting (not the List<PlayerControl> overload's implicit
                // MeetingCheck.Ignore default) - a remote client already on the meeting screen
                // from a report/emergency RPC race must not still apply this kill.
                source.RpcSpecialMultiMurder(filtered, MeetingCheck.OutsideMeeting, true, teleportMurderer: false,
                    playKillSound: true, causeOfDeath: "SuperSquadDetonator");
            }
        }

        target.RemoveModifier(bomb);
    }
}
