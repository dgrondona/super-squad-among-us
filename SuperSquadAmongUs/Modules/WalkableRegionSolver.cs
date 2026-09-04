using System.Collections.Generic;
using TownOfUs.Modules.Components;
using UnityEngine;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// Finds the reachable point closest to a target by walking a grid of small steps outward from
/// known-good seed points, rather than classifying the target point in isolation. Full rationale and
/// history: docs/roles/apparater.md.
/// </summary>
/// <remarks>
/// Traversal is deliberately blind to doors (a closed door is a solid collider, so an unfiltered
/// search can't path into a door-sealed room), while landing is not. Door state is only ever read,
/// never written - see docs/il2cpp-gotchas.md.
/// </remarks>
internal static class WalkableRegionSolver
{
    private const float MinCellSize = 0.15f;
    private const float MaxCellSize = 0.25f;
    private const int MaxExpandedCells = 8000;
    private const int AnchorRingSamples = 16;
    private const float FallbackProbeRadius = 0.2f;

    // Physics query result buffers. Sized generously because a saturated buffer has to be treated as
    // blocked (see IsOpen): ship geometry is many short EdgeCollider2D segments that bunch up at
    // corners and doorframes, so a handful of simultaneous overlaps is normal, not pathological.
    private const int HitBufferSize = 16;

    // "Stand in front of the vent, not inside its sprite" - TOU-Mira's own offset, used unvalidated by
    // DisperserModifier and Extensions.SpawnAtRandomVent.
    private const float VentStandOffset = 0.3636f;

    // Random-destination search: how many raw targets to try, and how far from a spawn center a raw
    // target may land (25 units spans every map from any of its spawn rings).
    private const int RandomAttempts = 3;
    private const float RandomTargetMaxRadius = 25f;

    // Landing needs extra clearance beyond the body's radius; traversal deliberately doesn't - see docs.
    private const float WallPadding = 0.1f;

    // Slightly under the body's true radius - the edge check is the real wall guard.
    private const float TraversalRadiusFactor = 0.9f;

    // 8-connected so a corner has a diagonal escape; diagonals are still edge-checked.
    private static readonly (int Dx, int Dy)[] Neighbors =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1),
        (1, 1), (1, -1), (-1, 1), (-1, -1),
    };

    /// <summary>Finds the reachable point nearest <paramref name="rawTarget"/>, seeded at <paramref name="origin"/> and at every known-good position on the map.</summary>
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
        var overlapBuffer = new Collider2D[HitBufferSize];
        var edgeBuffer = new RaycastHit2D[HitBufferSize];
        var doorColliderIds = CollectDoorColliderIds();
        var traversalRadius = probeRadius * TraversalRadiusFactor;
        var landingRadius = probeRadius + WallPadding;

        Vector2 CellCenter((int Cx, int Cy) cell) => origin + new Vector2(cell.Cx * cellSize, cell.Cy * cellSize);

        // "Open" means nothing is there, or - when ignoring doors - the only things there are doors.
        // A saturated buffer must fail closed: the buffer overloads fill up to the array length and
        // report that count with no signal that more colliders overlapped, so a real wall sitting
        // outside the truncated window would be invisible and we'd wrongly report open.
        bool IsOpen(Vector2 center, float radius, bool ignoreDoors)
        {
            var count = Physics2D.OverlapCircle(center, radius, filter, overlapBuffer);
            if (count == 0)
            {
                return true;
            }

            if (!ignoreDoors || count >= overlapBuffer.Length)
            {
                return false;
            }

            for (var i = 0; i < count; i++)
            {
                if (!IsDoor(overlapBuffer[i]))
                {
                    return false;
                }
            }

            return true;
        }

        // Positional Linecast, never a collider-based cast - the collider-based overloads sweep that
        // collider's own live position instead of the from/to passed in. See docs/roles/apparater.md.
        // The multi-result overload is required: the single-hit form returns only the CLOSEST collider,
        // so it would clear an edge whenever the nearest thing on it is a door with a wall just behind.
        bool HasClearEdge(Vector2 from, Vector2 to)
        {
            var count = Physics2D.Linecast(from, to, filter, edgeBuffer);
            if (count == 0)
            {
                return true;
            }

            if (count >= edgeBuffer.Length)
            {
                return false;
            }

            for (var i = 0; i < count; i++)
            {
                if (!IsDoor(edgeBuffer[i].collider))
                {
                    return false;
                }
            }

            return true;
        }

        bool IsDoor(Collider2D collider) => collider && doorColliderIds.Contains(collider.GetInstanceID());

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

        // Extra seeds, independent of the player's position, so a region the player can't walk to (a
        // door-sealed room, an Airship section joined only by a ladder) is still searchable. Every seed
        // is landing-validated first, so a bad one is silently dropped rather than used.
        // See docs/roles/apparater.md.
        foreach (var seedPoint in CollectSeedPoints(point => IsOpen(point, landingRadius, false)))
        {
            var seedCell = (Cx: Mathf.RoundToInt((seedPoint.x - origin.x) / cellSize),
                            Cy: Mathf.RoundToInt((seedPoint.y - origin.y) / cellSize));
            var seedCenter = CellCenter(seedCell);

            if (seedCell == originCell || !IsOpen(seedCenter, traversalRadius, true) || !visited.Add(seedCell))
            {
                continue;
            }

            var seedSqrDist = (seedCenter - rawTarget).sqrMagnitude;
            if (seedSqrDist < bestSqrDist && IsOpen(seedCenter, landingRadius, false))
            {
                bestSqrDist = seedSqrDist;
                bestCell = seedCell;
            }

            frontier.Enqueue(seedCell, seedSqrDist);
        }

        var expanded = 0;
        while (frontier.Count > 0 && expanded < MaxExpandedCells)
        {
            frontier.TryDequeue(out var curCell, out _);
            expanded++;

            var curCenter = CellCenter(curCell);

            if ((curCenter - rawTarget).sqrMagnitude <= earlySuccessSqrDist && IsOpen(curCenter, landingRadius, false))
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

                if (!IsOpen(neighborCenter, traversalRadius, true) || !HasClearEdge(curCenter, neighborCenter))
                {
                    continue;
                }

                var sqrDist = (neighborCenter - rawTarget).sqrMagnitude;
                if (sqrDist < bestSqrDist && IsOpen(neighborCenter, landingRadius, false))
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
            IsOpen(rawTarget, landingRadius, false) && HasClearEdge(result, rawTarget))
        {
            result = rawTarget;
        }

        return bestCell != originCell;
    }

    /// <summary>
    /// Finds a reachable point at a random spot on the map by aiming <see cref="TryFindReachablePoint"/>
    /// at random targets scattered around the map's spawn centers. Prefers a point at least
    /// <paramref name="minDistance"/> from <paramref name="origin"/>; falls back to the farthest
    /// candidate found. Pure randomness is fine here - exactly one client computes the destination and
    /// syncs it with a snap.
    /// </summary>
    /// <returns><see langword="true"/> if any reachable point other than <paramref name="origin"/> was found.</returns>
    public static bool TryFindRandomReachablePoint(Vector2 origin, float probeRadius, float minDistance, out Vector2 result)
    {
        result = origin;
        var ship = ShipStatus.Instance;
        if (!ship)
        {
            return false;
        }

        var centers = new[] { ship.MeetingSpawnCenter, ship.InitialSpawnCenter, ship.MeetingSpawnCenter2 };
        var found = false;
        var bestSqrDist = -1f;
        var minSqrDist = minDistance * minDistance;

        for (var attempt = 0; attempt < RandomAttempts; attempt++)
        {
            var center = centers[UnityEngine.Random.Range(0, centers.Length)];
            var rawTarget = center + (UnityEngine.Random.insideUnitCircle * RandomTargetMaxRadius);

            if (!TryFindReachablePoint(origin, rawTarget, probeRadius, out var candidate))
            {
                continue;
            }

            var sqrDist = (candidate - origin).sqrMagnitude;
            if (sqrDist >= minSqrDist)
            {
                result = candidate;
                return true;
            }

            if (sqrDist > bestSqrDist)
            {
                bestSqrDist = sqrDist;
                result = candidate;
                found = true;
            }
        }

        return found;
    }

    /// <summary>Gets the probe radius matching a player's collider (their body's half-extents), for feeding the solver.</summary>
    public static float GetProbeRadius(PlayerControl player)
    {
        var collider = player.Collider;
        return collider ? Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y) : FallbackProbeRadius;
    }

    /// <summary>
    /// Instance IDs of every door's blocking collider, so traversal can path through a closed one.
    /// </summary>
    /// <remarks>
    /// Resolved per search, never cached: TOU-Mira destroys and re-adds door components per map at
    /// OnEnable (MapDoorPatches), so anything captured at map load goes stale. Sweeping components
    /// rather than trusting ShipStatus.AllDoors is deliberate - that array is OpenableDoor[], which
    /// excludes Airship's ManualDoor, and AutoOpenMushroomDoor keeps its collider in its own
    /// wallCollider field while inheriting an unassigned myCollider from PlainDoor.
    /// </remarks>
    private static HashSet<int> CollectDoorColliderIds()
    {
        var ids = new HashSet<int>();
        var ship = ShipStatus.Instance;
        if (!ship)
        {
            return ids;
        }

        void Add(Collider2D? collider)
        {
            if (collider)
            {
                ids.Add(collider!.GetInstanceID());
            }
        }

        foreach (var door in ship.GetComponentsInChildren<PlainDoor>(true))
        {
            Add(door.myCollider);
        }

        foreach (var door in ship.GetComponentsInChildren<ManualDoor>(true))
        {
            Add(door.myCollider);
        }

        foreach (var door in ship.GetComponentsInChildren<MushroomWallDoor>(true))
        {
            Add(door.wallCollider);
        }

        // Installed on Fungle by TOU-Mira when the host picks Skeld-style doors; derives from
        // AutoOpenDoor, so the PlainDoor sweep above finds it but reads a null myCollider.
        foreach (var door in ship.GetComponentsInChildren<AutoOpenMushroomDoor>(true))
        {
            Add(door.wallCollider);
        }

        return ids;
    }

    /// <summary>
    /// Known-good standing positions to seed the search from, so regions the player can't walk to are
    /// still searchable. Only points passing <paramref name="isValidLanding"/> are returned.
    /// </summary>
    /// <remarks>
    /// One seed per connected region is all the search needs, so the spawn rings stop at their first
    /// clear sample - all of a ring's points are in the same region anyway. Vents and link endpoints
    /// each get their own test, since any one of them may be the only way into its region.
    /// </remarks>
    private static List<Vector2> CollectSeedPoints(Func<Vector2, bool> isValidLanding)
    {
        var seeds = new List<Vector2>();
        var ship = ShipStatus.Instance;
        if (!ship)
        {
            return seeds;
        }

        void AddIfValid(Vector2 point)
        {
            if (isValidLanding(point))
            {
                seeds.Add(point);
            }
        }

        // Vanilla spawns players ON the ring, never at the center - the center is the meeting table.
        var centers = new[] { ship.MeetingSpawnCenter, ship.InitialSpawnCenter, ship.MeetingSpawnCenter2 };
        foreach (var center in centers)
        {
            for (var i = 0; i < AnchorRingSamples; i++)
            {
                var angle = i * (360f / AnchorRingSamples);
                var candidate = center + (Vector2)(Quaternion.Euler(0f, 0f, angle) * (Vector2.up * ship.SpawnRadius));
                if (isValidLanding(candidate))
                {
                    seeds.Add(candidate);
                    break;
                }
            }
        }

        // Vents are authored standing positions, so they're valid by construction and cover every
        // section of every vanilla map.
        foreach (var vent in ship.AllVents.Where(vent => vent))
        {
            AddIfValid((Vector2)vent.transform.position + new Vector2(0f, VentStandOffset));
        }

        // Link endpoints, for sections a grid walk can't reach and the vents-everywhere assumption
        // might not cover (notably custom maps). An endpoint that isn't standable is simply dropped.
        foreach (var ladder in ship.Ladders)
        {
            if (!ladder)
            {
                continue;
            }

            AddIfValid(ladder.transform.position);
            if (ladder.Destination)
            {
                AddIfValid(ladder.Destination.transform.position);
            }
        }

        var airship = ship.TryCast<AirshipStatus>();
        if (airship && airship!.GapPlatform)
        {
            AddIfValid(airship.GapPlatform.LeftUsePosition);
            AddIfValid(airship.GapPlatform.RightUsePosition);
        }

        var fungle = ship.TryCast<FungleShipStatus>();
        if (fungle && fungle!.Zipline)
        {
            var zipline = fungle.Zipline;
            if (zipline.landingPositionTop)
            {
                AddIfValid(zipline.landingPositionTop.position);
            }

            if (zipline.landingPositionBottom)
            {
                AddIfValid(zipline.landingPositionBottom.position);
            }
        }

        return seeds;
    }
}
