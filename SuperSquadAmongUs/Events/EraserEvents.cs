using AmongUs.GameOptions;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.Hud;
using MiraAPI.Modifiers;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using SuperSquadAmongUs.Buttons.Impostor;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Modules;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs.Utilities;

namespace SuperSquadAmongUs.Events;

/// <summary>
/// Resolves the Eraser's marks at the meeting's exile screen (TOR's ExileControllerPatch hook):
/// every marked player's modded role is stripped - they become a plain vanilla Crewmate, even a
/// former impostor or neutral. Modifiers are deliberately NOT cleared (TOR keeps Lovers/Mini/etc.
/// through an erase). No notification is shown.
/// </summary>
public static class EraserEvents
{
    /// <summary>
    /// Resets the erase cooldown escalation at game start (TOR only clears it on game reset; the
    /// button singleton outlives games).
    /// </summary>
    [RegisterEvent]
    public static void RoundStartEventHandler(RoundStartEvent @event)
    {
        if (@event.TriggeredByIntro)
        {
            CustomButtonSingleton<EraserEraseButton>.Instance.ResetCooldownAddition();
        }
    }

    [RegisterEvent]
    public static void EjectionEventHandler(EjectionEvent @event)
    {
        foreach (var erased in ModifierUtils.GetActiveModifiers<FutureErasedModifier>().ToList())
        {
            var target = erased.Player;
            erased.ModifierComponent?.RemoveModifier(erased);
            EraseRole(target);
        }
    }

    /// <summary>
    /// RPC entry point for <see cref="EraserEraseButton"/>'s immediate-erase option (TOR's
    /// <c>EraseImmediately</c>). Unlike the deferred (next-meeting) path - which syncs by adding a
    /// <see cref="FutureErasedModifier"/> that every client resolves for itself at ejection -
    /// an immediate erase has no synced state to key off of, so it needs its own RPC to reach the host
    /// when a non-host Eraser clicks: calling <see cref="EraseRole"/> directly from the button ran
    /// fine locally but never sent anything, since the actual role change only fires under an
    /// <c>AmHost</c> check.
    /// </summary>
    /// <param name="source">The Eraser (or ability-grant holder) triggering the erase.</param>
    /// <param name="target">The player to strip.</param>
    [MethodRpc((uint)SuperSquadRpc.EraserErase, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcEraseRole(PlayerControl source, PlayerControl target)
    {
        if (!AbilityGrants.SenderIsOrHolds<EraserRole>(source))
        {
            Error("RpcEraseRole - Invalid Eraser");
            return;
        }

        EraseRole(target);
    }

    /// <summary>
    /// Strips a player's modded role, turning them into a plain Crewmate. Shared by the meeting-exile
    /// resolution above and by <see cref="RpcEraseRole"/> (the button's immediate-erase option).
    /// </summary>
    private static void EraseRole(PlayerControl? target)
    {
        // TOR technically "erases" dead players too, but there it's just static-list bookkeeping;
        // for us a role change on a dead player would replace their ghost role with a living one,
        // so the dead are skipped. TOR does NOT check the eraser's own state at resolution -
        // marks fire even if the eraser died or was erased - and neither do we.
        if (target == null || target.Data == null || target.Data.Disconnected || target.HasDied())
        {
            return;
        }

        // Role changes are RPCs - send exactly once, from the host (TOR's authority model). Both
        // call sites above (RpcEraseRole and the ejection handler) run on every client, so this
        // guard is what keeps the actual RpcChangeRole send singular.
        if (AmongUsClient.Instance && AmongUsClient.Instance.AmHost)
        {
            target.RpcChangeRole((ushort)RoleTypes.Crewmate);
        }
    }
}
