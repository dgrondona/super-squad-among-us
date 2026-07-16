using AmongUs.GameOptions;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.Hud;
using MiraAPI.Modifiers;
using SuperSquadAmongUs.Buttons.Impostor;
using SuperSquadAmongUs.Modifiers;
using TownOfUs.Utilities;

namespace SuperSquadAmongUs.Events;

/// <summary>
/// Resolves the Eraser's marks at the meeting's exile screen (TOR's ExileControllerPatch hook):
/// every marked player's modded role is stripped - they become a plain vanilla Crewmate, even a
/// former impostor or neutral (TOR-faithful, user decision 2026-07-16). Modifiers are deliberately
/// NOT cleared (TOR keeps Lovers/Mini/etc. through an erase). No notification is shown.
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

            // TOR technically "erases" dead players too, but there it's just static-list bookkeeping;
            // for us a role change on a dead player would replace their ghost role with a living one,
            // so the dead are skipped. TOR does NOT check the eraser's own state at resolution -
            // marks fire even if the eraser died or was erased - and neither do we.
            if (target == null || target.Data == null || target.Data.Disconnected || target.HasDied())
            {
                continue;
            }

            // Role changes are RPCs - send exactly once, from the host (TOR's authority model).
            if (AmongUsClient.Instance && AmongUsClient.Instance.AmHost)
            {
                target.RpcChangeRole((ushort)RoleTypes.Crewmate);
            }
        }
    }
}
