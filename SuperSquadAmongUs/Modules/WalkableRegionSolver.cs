using System.Collections.Generic;
using UnityEngine;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// Finds the reachable point closest to a target by walking a grid of small steps outward from
/// known-good seed points, rather than classifying the target point in isolation. Full rationale and
/// history: docs/roles/apparater.md.
/// </summary>
internal static class WalkableRegionSolver
{
    private const float MinCellSize = 0.15f;
    private const float MaxCellSize = 0.25f;
    private const int MaxExpandedCells = 8000;
    private const int AnchorRingSamples = 16;

    // Landing needs extra clearance beyond the body's radius; traversal deliberately doesn't - see docs.
    private const float WallPadding = 0.1f;

    // Slightly under the body's true radius - AnythingBetween (the edge check) is the real wall guard.
    private const float TraversalRadiusFactor = 0.9f;

    // 8-connected so a corner has a diagonal escape; diagonals are still edge-checked.
    private static readonly (int Dx, int Dy)[] Neighbors =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1),
        (1, 1), (1, -1), (-1, 1), (-1, -1),
    };

    /// <summary>Finds the reachable point nearest <paramref name="rawTarget"/>, seeded at <paramref name="origin"/> and a map spawn-ring anchor.</summary>
    /// <returns><see langword="true"/> if a reachable point other than <paramref name="origin"/> was found.</returns>
    public static bool TryFindReachablePoint(Vector2 origin, Vector2 rawTarget, float probeRadius, out Vector2 result)
    {
        var cellSize = Mathf.Clamp(probeRadius, MinCellSize, MaxCellSize);
        var mask = Constants.ShipAndAllObjectsMask;
        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = mask,
            useTriggers = false,
        };
        var overlapBuffer = new Collider2D[1];
        var traversalRadius = probeRadius * TraversalRadiusFactor;
        var landingRadius = probeRadius + WallPadding;

        Vector2 CellCenter((int Cx, int Cy) cell) => origin + new Vector2(cell.Cx * cellSize, cell.Cy * cellSize);

        bool IsOpen(Vector2 center, float radius) => Physics2D.OverlapCircle(center, radius, filter, overlapBuffer) == 0;

        // Collider-less overload deliberately - the collider-based one sweeps that collider's own live
        // position, not the from/to positions passed in. See docs/roles/apparater.md.
        bool HasClearEdge(Vector2 from, Vector2 to) => !PhysicsHelpers.AnythingBetween(from, to, mask, false);

        var originCell = (Cx: 0, Cy: 0);
        var visited = new HashSet<(int Cx, int Cy)>(MaxExpandedCells) { originCell };
        var frontier = new PriorityQueue<(int Cx, int Cy), float>(MaxExpandedCells);

        var bestCell = originCell;
        var bestSqrDist = (origin - rawTarget).sqrMagnitude;
        var earlySuccessSqrDist = cellSize * cellSize;

        if (bestSqrDist <= earlySuccessSqrDist)
        {
            result = origin;
            return false;
        }

        frontier.Enqueue(originCell, bestSqrDist);

        // Second seed, independent of the player's position - see docs/roles/apparater.md for why.
        if (TryFindSpawnAnchor(landingRadius, IsOpen, out var anchorPoint))
        {
            var anchorCell = (Cx: Mathf.RoundToInt((anchorPoint.x - origin.x) / cellSize),
                              Cy: Mathf.RoundToInt((anchorPoint.y - origin.y) / cellSize));
            var anchorCenter = CellCenter(anchorCell);

            if (anchorCell != originCell && IsOpen(anchorCenter, traversalRadius) && visited.Add(anchorCell))
            {
                var anchorSqrDist = (anchorCenter - rawTarget).sqrMagnitude;
                if (anchorSqrDist < bestSqrDist && IsOpen(anchorCenter, landingRadius))
                {
                    bestSqrDist = anchorSqrDist;
                    bestCell = anchorCell;
                }

                frontier.Enqueue(anchorCell, anchorSqrDist);
            }
        }

        var expanded = 0;
        while (frontier.Count > 0 && expanded < MaxExpandedCells)
        {
            frontier.TryDequeue(out var curCell, out _);
            expanded++;

            var curCenter = CellCenter(curCell);

            if ((curCenter - rawTarget).sqrMagnitude <= earlySuccessSqrDist && IsOpen(curCenter, landingRadius))
            {
                bestCell = curCell;
                break;
            }

            foreach (var (dx, dy) in Neighbors)
            {
                var neighborCell = (Cx: curCell.Cx + dx, Cy: curCell.Cy + dy);
                if (!visited.Add(neighborCell))
                {
                    continue;
                }

                var neighborCenter = CellCenter(neighborCell);

                if (!IsOpen(neighborCenter, traversalRadius) || !HasClearEdge(curCenter, neighborCenter))
                {
                    continue;
                }

                var sqrDist = (neighborCenter - rawTarget).sqrMagnitude;
                if (sqrDist < bestSqrDist && IsOpen(neighborCenter, landingRadius))
                {
                    bestSqrDist = sqrDist;
                    bestCell = neighborCell;
                }

                frontier.Enqueue(neighborCell, sqrDist);
            }
        }

        result = CellCenter(bestCell);

        // Precision polish: land exactly on the click if the winning cell is right next to it.
        if (bestCell != originCell && (result - rawTarget).sqrMagnitude <= earlySuccessSqrDist &&
            IsOpen(rawTarget, landingRadius) && HasClearEdge(result, rawTarget))
        {
            result = rawTarget;
        }

        return bestCell != originCell;
    }

    /// <summary>Finds a clear point on the map's spawn ring(s) to use as a position-independent search seed.</summary>
    private static bool TryFindSpawnAnchor(float clearRadius, Func<Vector2, float, bool> isOpen, out Vector2 anchor)
    {
        var ship = ShipStatus.Instance;
        if (!ship)
        {
            anchor = default;
            return false;
        }

        var centers = new[] { ship.MeetingSpawnCenter, ship.InitialSpawnCenter, ship.MeetingSpawnCenter2 };

        foreach (var center in centers)
        {
            for (var i = 0; i < AnchorRingSamples; i++)
            {
                var angle = i * (360f / AnchorRingSamples);
                var candidate = center + (Vector2)(Quaternion.Euler(0f, 0f, angle) * (Vector2.up * ship.SpawnRadius));
                if (isOpen(candidate, clearRadius))
                {
                    anchor = candidate;
                    return true;
                }
            }

            // Ring centers are usually obstructed (e.g. the meeting table) - last resort only.
            if (isOpen(center, clearRadius))
            {
                anchor = center;
                return true;
            }
        }

        anchor = default;
        return false;
    }
}
