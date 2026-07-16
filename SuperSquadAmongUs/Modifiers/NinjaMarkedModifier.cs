using TownOfUs.Modifiers;
using UnityEngine;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// Placed on the Ninja's marked target, LOCALLY on the Ninja's client only - the arrow it renders is
/// for the Ninja's eyes alone (TOR convention: the tracking arrow is client-side UI, never visible to
/// other players, cameras, or admin). Never auto-expires; removed when the mark is consumed/cleared.
/// </summary>
public sealed class NinjaMarkedModifier(PlayerControl owner, Color color, float updateInterval)
    : ArrowTargetModifier(owner, color, updateInterval)
{
    /// <inheritdoc />
    public override string ModifierName => "Ninja Marked";

    /// <summary>
    /// Marks don't survive meetings; the modifier removes itself (and its arrow) when one starts.
    /// </summary>
    public override void OnMeetingStart()
    {
        ModifierComponent!.RemoveModifier(this);
    }
}
