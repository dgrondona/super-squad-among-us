using MiraAPI.Events;
using MiraAPI.Events.Mira;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Hud;
using MiraAPI.Modifiers;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Modules;
using TownOfUs.Modifiers;

namespace SuperSquadAmongUs.Events;

/// <summary>
/// The Elusive's shield interception (Veteran's exact pattern, teleport instead of kill): while the
/// Elusive is shielded, a kill attempt or targeted ability against them is cancelled and the attacker
/// is whisked to a random reachable spot on the map. The cancel runs deterministically on every client
/// (shield state arrived via synced modifiers); only the attacker's own client computes the random
/// destination and snaps, since movement is client-authoritative.
/// </summary>
public static class ElusiveEvents
{
    // The teleport should feel like being thrown across the map, not shuffled sideways.
    private const float MinTeleportDistance = 5f;

    /// <summary>
    /// Local-only: a targeted ability button (kill or otherwise) clicked on a shielded Elusive.
    /// Cancelling here prevents ClickHandler entirely - no cooldown or use is consumed.
    /// </summary>
    [RegisterEvent(1)]
    public static void MiraButtonClickEventHandler(MiraButtonClickEvent @event)
    {
        var button = @event.Button as CustomActionButton<PlayerControl>;
        var source = PlayerControl.LocalPlayer;
        var target = button?.Target;

        if (target == null || button == null || !button.CanClick())
        {
            return;
        }

        CheckForElusiveShield(@event, source, target);
    }

    /// <summary>
    /// All clients: a murder attempt against a shielded Elusive.
    /// </summary>
    [RegisterEvent(1)]
    public static void BeforeMurderEventHandler(BeforeMurderEvent @event)
    {
        CheckForElusiveShield(@event, @event.Source, @event.Target);
    }

    private static void CheckForElusiveShield(MiraCancelableEvent miraEvent, PlayerControl source, PlayerControl target)
    {
        if (MeetingHud.Instance || ExileController.Instance)
        {
            return;
        }

        if (source == target || !target.HasModifier<ElusiveShieldModifier>())
        {
            return;
        }

        var isIndirect = source.TryGetModifier<IndirectAttackerModifier>(out var indirectMod);
        if (indirectMod is { IgnoreShield: true })
        {
            // Shield-piercing indirect attacks pass through, mirroring the Veteran.
            return;
        }

        miraEvent.Cancel();

        // Veteran parity: indirect attackers (e.g. an Arsonist's douse) are blocked but not punished.
        // No InvulnerabilityModifier carve-out - unlike a retaliation kill, teleporting a Pestilence
        // is harmless.
        if ((TutorialManager.InstanceExists || source.AmOwner) && !isIndirect)
        {
            var origin = source.GetTruePosition();
            var probeRadius = WalkableRegionSolver.GetProbeRadius(source);

            if (WalkableRegionSolver.TryFindRandomReachablePoint(origin, probeRadius, MinTeleportDistance, out var destination))
            {
                source.NetTransform.RpcSnapTo(destination);
            }
        }
    }
}
