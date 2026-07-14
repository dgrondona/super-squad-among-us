using System.Collections.Generic;
using UnityEngine;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// Finds the reachable point closest to a target by walking a grid of small steps outward from a
/// known-good origin, rather than classifying the target point in isolation.
/// </summary>
/// <remarks>
/// A point sitting on the far side of a thin wall (or embedded in a wall-attached obstacle, e.g. a
/// console built into a wall) isn't inside any collider, so no point/circle overlap test can ever see
/// it as blocked. Only a chain of validated short steps from somewhere legitimately walkable can
/// guarantee a destination is actually reachable without a step crossing a wall - which is what this
/// does: a greedy best-first search (ordered by distance to the raw target) through 4-connected grid
/// cells, seeded at <paramref name="origin"/>, accepting a cell only if both the cell itself is clear
/// of solid obstacles and the short step onto it doesn't cross a wall.
/// </remarks>
internal static class WalkableRegionSolver
{
    private const float MinCellSize = 0.15f;
    private const float MaxCellSize = 0.25f;
    private const int MaxExpandedCells = 8000;

    // Extra clearance added on top of the body's own radius for the per-cell obstacle check, so a
    // "clear" cell has real breathing room from a wall/obstacle rather than merely not overlapping it
    // (which can still read as "standing in the wall" once the body actually occupies that spot).
    private const float WallPadding = 0.1f;

    private static readonly (int Dx, int Dy)[] Neighbors = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    /// <summary>
    /// Finds the reachable point nearest <paramref name="rawTarget"/>, walking outward from
    /// <paramref name="origin"/> (which must already be a valid, walkable position).
    /// </summary>
    /// <param name="origin">A known-good, already-walkable starting position (e.g. the player's own position).</param>
    /// <param name="rawTarget">The unvalidated target position to snap toward.</param>
    /// <param name="probeRadius">The radius of the moving body, used both as the grid cell size and the per-cell obstacle check radius.</param>
    /// <param name="selfCollider">The collider used for the wall-crossing check between adjacent cells.</param>
    /// <param name="result">The reachable point found, closest to <paramref name="rawTarget"/>.</param>
    /// <returns><see langword="true"/> if a reachable point other than <paramref name="origin"/> itself was found.</returns>
    public static bool TryFindReachablePoint(Vector2 origin, Vector2 rawTarget, float probeRadius, Collider2D selfCollider, out Vector2 result)
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
        var paddedRadius = probeRadius + WallPadding;

        Vector2 CellCenter((int Cx, int Cy) cell) => origin + new Vector2(cell.Cx * cellSize, cell.Cy * cellSize);

        bool IsCellOpen(Vector2 center) => Physics2D.OverlapCircle(center, paddedRadius, filter, overlapBuffer) == 0;

        bool HasClearEdge(Vector2 from, Vector2 to) => !PhysicsHelpers.AnythingBetween(selfCollider, from, to, mask, false);

        var originCell = (Cx: 0, Cy: 0);
        var visited = new HashSet<(int Cx, int Cy)>(MaxExpandedCells) { originCell };
        var frontier = new PriorityQueue<(int Cx, int Cy), float>(MaxExpandedCells);

        var bestCell = originCell;
        var bestSqrDist = (origin - rawTarget).sqrMagnitude;
        var earlySuccessSqrDist = cellSize * cellSize;
        frontier.Enqueue(originCell, bestSqrDist);

        var expanded = 0;
        while (frontier.Count > 0 && expanded < MaxExpandedCells)
        {
            frontier.TryDequeue(out var curCell, out _);
            expanded++;

            var curCenter = CellCenter(curCell);

            if ((curCenter - rawTarget).sqrMagnitude <= earlySuccessSqrDist)
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

                if (!IsCellOpen(neighborCenter) || !HasClearEdge(curCenter, neighborCenter))
                {
                    continue;
                }

                var sqrDist = (neighborCenter - rawTarget).sqrMagnitude;
                if (sqrDist < bestSqrDist)
                {
                    bestSqrDist = sqrDist;
                    bestCell = neighborCell;
                }

                frontier.Enqueue(neighborCell, sqrDist);
            }
        }

        result = CellCenter(bestCell);

        // Precision polish: if the winning cell is right next to the raw target, try landing on the
        // exact click instead of the cell center.
        if (bestCell != originCell && (result - rawTarget).sqrMagnitude <= earlySuccessSqrDist &&
            IsCellOpen(rawTarget) && HasClearEdge(result, rawTarget))
        {
            result = rawTarget;
        }

        return bestCell != originCell;
    }
}
