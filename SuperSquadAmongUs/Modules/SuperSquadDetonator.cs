using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
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
    /// sends the kill (<see cref="RpcSpecialMultiMurder"/> is itself an RPC).
    /// </summary>
    /// <param name="source">The Detonator.</param>
    /// <param name="target">The bombed player.</param>
    [MethodRpc((uint)SuperSquadRpc.DetonatorDetonate, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcDetonate(PlayerControl source, PlayerControl target)
    {
        if (source.Data.Role is not DetonatorRole)
        {
            Error("RpcDetonate - Invalid Detonator");
            return;
        }

        if (MeetingHud.Instance || ExileController.Instance)
        {
            if (target.HasModifier<DetonatorBombModifier>())
            {
                target.RemoveModifier<DetonatorBombModifier>();
            }

            return;
        }

        if (source.AmOwner)
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
                source.RpcSpecialMultiMurder(filtered, true, teleportMurderer: false, playKillSound: true,
                    causeOfDeath: "SuperSquadDetonator");
            }
        }

        if (target.HasModifier<DetonatorBombModifier>())
        {
            target.RemoveModifier<DetonatorBombModifier>();
        }
    }
}
