using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Modifiers;
using SuperSquadAmongUs.Modifiers;

namespace SuperSquadAmongUs.Events;

/// <summary>
/// Tracks whether the Astral killed anyone during their current phase, for
/// <see cref="Options.Roles.Impostor.AstralOptions.DieWithoutKill"/>. Fires on every client
/// (<see cref="AfterMurderEvent"/> is broadcast for every murder, not just the killer's own), so the
/// flag stays in agreement everywhere by the time <see cref="AstralFormModifier.OnDeactivate"/> checks it.
/// </summary>
public static class AstralEvents
{
    [RegisterEvent]
    public static void AfterMurderEventHandler(AfterMurderEvent @event)
    {
        if (@event.Source != null && @event.Source.TryGetModifier<AstralFormModifier>(out var form))
        {
            form.HasKilled = true;
        }
    }
}
