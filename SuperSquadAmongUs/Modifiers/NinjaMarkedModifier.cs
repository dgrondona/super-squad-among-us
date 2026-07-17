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
    // TOU-Mira's shared arrow prefab is a bit large for a persistent tracking indicator; shrink it a
    // little for this use (the shared prefab's scale isn't exposed as a parameter on CreateArrow).
    private const float ArrowScale = 0.75f;

    private Vector3 targetScale = Vector3.one;

    /// <inheritdoc />
    public override string ModifierName => "Ninja Marked";

    /// <inheritdoc />
    public override void OnActivate()
    {
        base.OnActivate();

        if (Arrow != null)
        {
            targetScale = Arrow.transform.localScale * ArrowScale;
            Arrow.transform.localScale = targetScale;
        }
    }

    /// <inheritdoc />
    public override void FixedUpdate()
    {
        base.FixedUpdate();

        // Re-assert every tick in case ArrowBehaviour.Update() resets scale (e.g. distance-based
        // pulsing), same self-heal approach used elsewhere for state vanilla code keeps overwriting.
        if (Arrow != null && Arrow.transform.localScale != targetScale)
        {
            Arrow.transform.localScale = targetScale;
        }
    }

    /// <summary>
    /// Marks don't survive meetings; the modifier removes itself (and its arrow) when one starts.
    /// </summary>
    public override void OnMeetingStart()
    {
        ModifierComponent!.RemoveModifier(this);
    }
}
