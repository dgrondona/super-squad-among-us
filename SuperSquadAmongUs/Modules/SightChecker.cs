using TownOfUs.Roles.Other;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// Checks whether any living player currently has line of sight to a target player's body.
/// </summary>
internal static class SightChecker
{
    // Offsets from the target's true position approximating the body sprite, so a sliver of the body
    // peeking past a corner is enough to count as "seen" - not just the center point.
    private static readonly Vector2[] SampleOffsets =
    {
        new(0f, 0f),
        new(0.25f, 0.45f),
        new(-0.25f, 0.45f),
        new(0.25f, -0.15f),
        new(-0.25f, -0.15f),
    };

    /// <summary>
    /// Returns <see langword="true"/> if any living, non-spectator player has an unobstructed, in-range
    /// view of any sample point on <paramref name="target"/>'s body.
    /// </summary>
    public static bool CanAnyoneSee(PlayerControl target)
    {
        if (!target)
        {
            return false;
        }

        // No live ship to raycast against - fail visible, the safer default.
        if (!ShipStatus.Instance)
        {
            return true;
        }

        var origin = target.GetTruePosition();

        foreach (var watcher in PlayerControl.AllPlayerControls)
        {
            // Dead players and spectators can't see anyone. In-vent/invisible/concealed players are
            // deliberately NOT skipped here - they still count as watchers.
            if (!watcher || watcher.PlayerId == target.PlayerId || watcher.Data == null ||
                watcher.HasDied() || watcher.Data.Role is SpectatorRole)
            {
                continue;
            }

            var visionRadius = ShipStatus.Instance.CalculateLightRadius(watcher.Data);
            var eye = watcher.GetTruePosition();

            foreach (var offset in SampleOffsets)
            {
                var samplePoint = origin + offset;

                // Collider-less overload deliberately - the collider-based one sweeps that collider's own
                // live position, not the from/to positions passed in. See docs/roles/apparater.md.
                if (Vector2.Distance(eye, samplePoint) <= visionRadius &&
                    !PhysicsHelpers.AnythingBetween(eye, samplePoint, Constants.ShadowMask, false))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
