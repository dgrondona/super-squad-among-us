# Apparater

Crewmate Power role. Ability opens the map (like the crewmate minimap / imposter sabotage map) and
teleports the player to wherever they click inside it.

Files: `Roles/Crewmate/ApparaterRole.cs`, `Buttons/Crewmate/ApparaterMapButton.cs`,
`Options/Roles/Crewmate/ApparaterOptions.cs`.

## Design decisions (confirmed with the user, don't re-litigate without a reason)

- **Movement while the map is open: allowed.** The player can keep walking around while picking a
  teleport destination (matches the `MoveWithMenu` option Spy's admin table supports elsewhere in
  TOU-Mira). If this ever needs to change, it's a one-line removal of freedom, not a redesign.
- **Map screen: a bare minimap** — ship layout + the player's own position dot only, no task overlay,
  no vent icons, no admin-style player/task counts. Task/vent/count info is irrelevant to picking a
  teleport spot and was reported as visual clutter in play-testing (see "Play-test fixes" below).

## How the click → world position conversion works

There's no built-in "click the map to teleport" mechanic in TOU-Mira or MiraAPI to copy — this is
built from a formula TOU-Mira uses elsewhere for a different purpose. TOU-Mira positions vent/dead-body
icons on the map with:

```csharp
icon.transform.localPosition = worldPos / ShipStatus.Instance.MapScale;
```

(see `reference/TOU-Mira/TownOfUs/Patches/Misc/MapBehaviourPatch.cs` and
`Modifiers/Game/Universal/SatelliteModifier.cs`). `ApparaterMapButton.GetRawClickWorldPosition` inverts
this: screen click → `Camera.main.ScreenToWorldPoint` → `InverseTransformPoint` relative to
`MapBehaviour.Instance.HerePoint.transform.parent` → `* ShipStatus.Instance.MapScale` → ship world
position. **Confirmed correct by play-testing** — teleports land where clicked, so this formula holds
for at least the map(s) tested so far. Not separately confirmed on multi-page maps (Polus/Airship/
Fungle have a "swipe" map with multiple sections) — if teleports ever land offset specifically on one
of those maps, that's the first place to look.

## Play-test fixes (round 1)

The user tested the role in a live game and reported three bugs, all now fixed:

1. **Map showed tasks and vent icons.** `ShowNormalMap()` is a high-level call — TOU-Mira's own
   `ShowVentsPatch` Harmony postfix specifically targets `ShowNormalMap`/`ShowCountOverlay`/
   `ShowSabotageMap` to overlay vent icons, and `ShowNormalMap()` also leaves the task overlay visible.
   Fix: call `MapBehaviour.Instance.GenericShow()` instead (the bare "show the ship layout" call,
   copying the pattern TOU-Mira's own `SpyAdminTableRoleButton` uses for its portable admin table), then
   manually configure the overlay state (`taskOverlay.Hide()`, `countOverlay.gameObject.SetActive(false)`,
   `HerePoint.enabled = true` + `TrackedHerePoint` disabled). `GenericShow()` isn't a Harmony patch
   target, so the vent-icon postfix never runs.
2. **Long delay between clicking and teleporting.** Root cause: `Input.GetMouseButtonDown(0)` is a
   single-frame flag scoped to Unity's `Update()`, but we were reading it from `FixedUpdate()` — if no
   fixed tick happens to land on the exact render frame of the click, the down-event is silently missed
   entirely (not delayed — dropped), and the player has to click repeatedly until one attempt happens to
   line up. This is a known Unity footgun; tellingly, MiraAPI's own `TeleportButton` example
   (`reference/MiraAPI/MiraAPI.Example/Buttons/Teleporter/TeleportButton.cs`) reads `Input.GetKey`
   (held-state), never `GetKeyDown`, in its own `FixedUpdate`, for this exact reason. Fix: poll
   `Input.GetMouseButton(0)` (safe to read from `FixedUpdate`) and do rising-edge detection ourselves
   with a `wasMouseDown` field, seeded to the real mouse state at `OnClick()` time so the same physical
   click that pressed the ability button can't double as the map-click (previously handled with a
   fragile "skip one tick" guard — this replaces that entirely).
3. **Could teleport into obstacles** (engines, tables, admin console, etc.) — first attempt, later
   found still broken; see "Play-test fixes (round 2)" below for the actual fix. `Helpers.GetRoom()`
   only checks a room's broad polygon, not solid furniture/console colliders inside it — a click
   that's "in the engine room" per that polygon can still be on top of the solid engine console
   collider, so a separate obstacle check is needed on top of the room check.

## Play-test fixes (round 2)

Round 1's obstacle check (`Physics2D.OverlapPointAll(point, Constants.ShipAndAllObjectsMask)`,
filtered to exclude trigger colliders and layers 5/8, copying the filter
`SentryPlaceCameraButton`/`MinerPlaceVentButton` use) did not actually stop teleporting into walls or
the Electrical console — confirmed by a second round of play-testing. Root cause: that filter pattern
doesn't fit this use case. Sentry/Miner run an **unmasked** `OverlapBoxAll` (all layers, no mask
argument) at the player's **own already-valid position**, so they need the extra `!isTrigger` +
"exclude layers 5/8" filtering to avoid mistaking themselves/UI/other unrelated hits for an obstacle.
Our query already passes `Constants.ShipAndAllObjectsMask`, which scopes to just the ship-structure +
object layers — copying their additional filtering on top of an already-masked query was redundant at
best, and at worst (if Among Us drives wall/console collision through trigger colliders plus custom
movement-blocking code, rather than physics-resolved solids, which is plausible for this kind of
top-down kinematic movement) it was silently excluding the real obstacle colliders via `!isTrigger`,
i.e. the check always reported "clear" no matter what was clicked.

Fix in `IsObstructed`: drop the `isTrigger`/layer filtering entirely and trust the mask — any collider
hit on `Constants.ShipAndAllObjectsMask` counts as an obstacle. Also switched from a zero-size point
check (`OverlapPointAll`) to `Physics2D.OverlapCircle(point, radius, mask)` sized to the local player's
actual collider (`PlayerControl.LocalPlayer.Collider.bounds.extents`), so the check is "would the
player's body fit here" rather than "is this exact mathematical point clear" (avoids teleporting so the
player's sprite still visually clips an obstacle's edge).

Round 2 was confirmed **partially** working by play-testing: it correctly blocked freestanding
furniture (admin table, meeting table), but did **not** block walls (clicking off the ship's playable
area still teleported into a wall) or wall-embedded obstacles (Electrical's breaker box, the engine
room obstacles — both of which the user noted are "attached to walls").

## Play-test fixes (round 3)

Researched (via a research-only subagent pass over both `reference/TOU-Mira` and `reference/MiraAPI`)
whether any existing role/feature in this modding ecosystem already solves "is an arbitrary point
walkable" — **nothing does**. Every teleport/placement feature in both codebases only ever validates
the *player's own current position* (already walkable by construction) or moves things to an
*already-occupied* position (a dead body, a swap partner). This is a genuinely new problem for this
codebase, not something to copy from elsewhere.

The most likely explanation for round 2's remaining gap: Among Us ship walls (and wall-embedded
obstacles like Electrical's console) are most likely thin boundary colliders (edge/line-like), not
filled solid volumes. A point/circle overlap test only registers a hit if the probe actually touches
that line — it has no concept of "inside," so a point just on the far side of a wall (or inside a
notch where an obstacle is embedded into the wall boundary) reads as "clear" even though no player
could ever actually stand there. That exactly matches the reported pattern: freestanding furniture
(its own real solid volume) got blocked correctly; wall-attached obstacles and the walls themselves
(thin boundary geometry) did not.

Fix in `IsValidTeleportPoint`: added a third check on top of the room-membership and overlap checks —
a **reachability** test using `PhysicsHelpers.AnythingBetween`, the same line-crossing primitive
TOU-Mira uses everywhere else for "is a wall in the way" (vent usability, kill line-of-sight,
placement validation for Sentry/Miner). Crossing-detection catches a thin wall between two points
regardless of which side is being tested, unlike a point-overlap test. The two points used are the
candidate and `room.roomArea.bounds.center` (the AABB center of the room the candidate resolved to,
via `Helpers.GetRoom`) as a "known open interior" reference point. If anything on
`Constants.ShipAndAllObjectsMask` crosses that segment, the candidate is rejected as sealed off behind
a wall (or embedded in one), and the ring-search moves on to the next candidate.

Also added `Info(...)` logging in `FixedUpdate` on every teleport attempt (both success and "no valid
point found") — if this round's fix is still incomplete, the log will show the raw click position and
whether/where the teleport landed, instead of needing another round of pure code-reading speculation.

**Not yet confirmed by play-testing as of this writing.** Known weak point in this approach: the
room's AABB `bounds.center` is a heuristic "definitely open" reference point, not a verified one — for
an irregularly-shaped or very cluttered room, the center itself could theoretically sit on/near an
obstacle, which would cause `AnythingBetween` to flag a false crossing for legitimate points on the far
side of that obstacle from the center. If a specific room shows over-aggressive rejection (ring-search
keeps sliding the player somewhere unexpected within a room that should have plenty of open floor),
that heuristic is the first thing to revisit — e.g. by validating the center itself first, or using
multiple candidate reference points.

**Also unconfirmed: hallways.** Per the round-3 research pass, `MiscUtils.GetRoomName`'s fallback
string for anywhere `Helpers.GetRoom` returns null is literally `"Outside/Hallway"` — suggesting
hallway segments may not have their own `PlainShipRoom`/`roomArea` entry at all, grouped with "outside
the ship" in this codebase's own terminology.

Both predictions from round 3 were confirmed by a third round of play-testing:
- **Hallways confirmed broken** — clicking in a corridor redirects to the nearest room instead of
  landing in the corridor, exactly as predicted from the `"Outside/Hallway"` fallback string.
- **Walls and wall-embedded obstacles were still not blocked** — the `AnythingBetween`-from-room-center
  reachability check had no observable effect; behavior was reported as identical to round 2.

## Play-test fixes (round 4) — hit the limit of static analysis

Two more research passes were done before touching code again:

1. A subagent searched both repos for any existing "click a room on the map" mechanism that could be
   reused wholesale (the sabotage map's room-clicking seemed like a promising, already-proven system to
   borrow). **Dead end** — the sabotage map's clickable regions (`ButtonBehavior.colliders`, wired to
   `MapRoom.SabotageX()` methods) only exist for the ~6–8 sabotageable rooms, are prefab-authored (not
   queryable per-room geometry), and resolve to a fixed action, never a location. `PlainShipRoom`
   confirmed to have only `RoomId`, `survCamera`, and `roomArea` — no per-room safe-standing anchor
   exists anywhere in either codebase to snap to instead.
2. Installed a decompiler (Mono.Cecil) and directly inspected the actual installed game's IL2CPP interop
   assembly (`~/.local/share/Steam/.../BepInEx/interop/Assembly-CSharp.dll`) to try to read the real
   numeric values of `Constants.ShipAndAllObjectsMask` etc. and the real logic of
   `PhysicsHelpers.AnythingBetween`. **Also a dead end, but a conclusive one**: IL2CPP interop assemblies
   contain only marshaling stubs — every field/method body just calls into native (C++, AOT-compiled)
   code via `GetIl2CppField`/native pointers. The actual mask bit-values and collision logic are not
   present in any inspectable form on disk. This is a hard boundary: **further certainty here requires
   the game to actually be running**, not more code reading.

Given that, this round makes one well-understood, purely-additive improvement, and otherwise pivots to
instrumentation rather than another guess at gating logic:

- **New check: `HasRoomMargin`.** `Helpers.GetRoom()` is a single-point-in-polygon test, which passes
  right up to a room polygon's boundary edge — exactly where "clicked on a wall" points tend to land,
  since the player's own body (not a mathematical point) needs to fit there. `HasRoomMargin` now also
  requires a ring of points one player-radius away in every direction to *also* resolve to some room
  (any room, not necessarily the same one, so it doesn't reject points near a legitimate doorway into a
  different room). This is a strictly-stricter, unambiguous change — it can only reject more points, not
  fewer — so unlike the last two rounds it can't have made anything worse, only mainly relevant if it
  turns out to help with the walls case.
- **Diagnostic logging, not another blind gating change.** `LogMasksOnce` logs the real (session-
  resolved) integer value of every `Constants.*Mask` plus `LayerMask.NameToLayer` for several candidate
  layer names, once per game. `LogClickDiagnostics` runs on every click and dumps *every* collider
  actually present at the clicked point — unmasked, all layers — with name/layer/isTrigger/tag, plus the
  individual result of every check (`GetRoom`, `HasRoomMargin`, `IsObstructed`) and, for comparison only
  (not used for gating — its return-value polarity is unverified), `PhysicsHelpers.CircleContains`, a
  same-purpose-sounding helper discovered during the Cecil inspection that TOU-Mira itself never calls.

**What's needed next: real log output, not another round of guessing.** Test these four spots and paste
back the BepInEx console log (or log file) for each click — that's the `Apparater: click at ...` block
and everything under it:
1. A click on open floor (expected: teleport succeeds, `IsObstructed=False`).
2. A click directly on a wall.
3. A click on the Electrical breaker-box console / an engine obstacle.
4. A click in a hallway, away from any room.

The collider dump from (2) and (3) will show definitively what's physically there (or isn't) — if
there's a collider present, its logged `layer`/`isTrigger` tells us exactly what the gating logic needs
to match; if there's *no* collider present at all at a point that's clearly a wall, that confirms the
thin-boundary-collider theory from round 3 and rules out a mask/layer mismatch as the explanation.

## Play-test fixes (round 5) — architectural rewrite: reachability instead of point classification

Rounds 1–4 were all variations on the same question: "is this exact clicked point valid?" That question
turned out to be fundamentally unanswerable for the failing cases — a point on the far side of a thin
wall, or embedded in a wall-attached console, is not inside any collider, so no point/circle test can
ever see it as blocked. The user reframed the problem: instead of classifying the destination in
isolation, ask "can I actually walk there from here, one short step at a time, without any step crossing
a wall?" — a connectivity question, which can't be tunneled through a wall the way independent point
testing (including the round 1–4 outward ring-search) can.

Validated via two more research passes (a codebase-precedent search and a from-scratch algorithm design
pass) before writing any code:
- Confirmed there's no existing flood-fill/BFS/pathfinding of any kind in either reference repo — this
  is genuinely new code, not something to adapt from elsewhere. Dummy/bot players don't path at all
  (`DummyBehaviour` only randomizes cosmetics; `ShipStatus.DummyLocations` is never referenced anywhere).
- Confirmed the player's collider is a `CircleCollider2D`, world diameter roughly 0.4–0.5 units, used to
  size the grid cells.
- Identified *why* round 2's `Physics2D.OverlapCircle` missed walls but round 3's
  `PhysicsHelpers.AnythingBetween` had no effect: `AnythingBetween` is a line-crossing check, which
  *can* see thin wall colliders that a circle overlap can't — round 3 just drew one long, mis-anchored
  line (room-polygon-bounds-center to the click) that could pass through a doorway gap and miss the
  actual wall segment. Applied as many *short* segments between adjacent grid cells instead, it becomes
  the reliable part of the fix.

**Replaced entirely:** `TryFindNearestValidPoint` (outward ring search), `IsValidTeleportPoint`,
`HasRoomMargin`/`MarginSampleCount`, and the room-center `AnythingBetween` check are all deleted from
`ApparaterMapButton.cs`. `Helpers.GetRoom()` is no longer part of the validation path at all — this is
what fixes hallways as a side effect (they have no `PlainShipRoom` entry, so anything routed through
`GetRoom` was always going to exclude them; pure geometry has no such gap).

**New: `SuperSquadAmongUs/Modules/WalkableRegionSolver.cs`** — `WalkableRegionSolver.TryFindReachablePoint`.
A greedy best-first search (`PriorityQueue<long,float>` ordered by squared distance to the raw click)
over a grid of ~0.15–0.25-unit cells, seeded at the player's own real position (`GetTruePosition()`,
always valid by construction) and expanding through 4-connected neighbors. A neighbor cell is only
accepted if **both**:
1. `Physics2D.OverlapCircle(cellCenter, probeRadius, mask)` finds nothing (`Constants.ShipAndAllObjectsMask`,
   `useTriggers=false`) — catches freestanding solids (the thing round 2 already proved works: admin
   table, meeting table).
2. `PhysicsHelpers.AnythingBetween(playerCollider, fromCenter, toCenter, mask, false)` finds nothing on
   the short step onto it — catches thin wall colliders (the thing rounds 2–4 couldn't).

Bounded by a hard cap (`MaxExpandedCells = 8000`) so a click clear across the ship can't cause a
multi-hundred-millisecond hitch; always returns the closest-to-click reachable cell seen, even if the
cap is hit or the click is genuinely unreachable, so it never "does nothing" while the player is
standing somewhere valid. `ApparaterMapButton.FixedUpdate` now calls this directly instead of the old
ring-search. The click→world coordinate conversion (`GetRawClickWorldPosition`) is unchanged.

**Diagnostics kept, trimmed:** `LogMasksOnce` and a slimmed `LogClickDiagnostics` (raw collider dump at
the click point, plus a direct `AnythingBetween(origin, click)` check) remain, since they're still the
only way to confirm live whether the two per-step checks above are actually seeing the walls. The
`HasRoomMargin`/`CircleContains`-comparison logging from round 4 is gone along with the code it was
diagnosing.

Play-tested: the reachability architecture is a real improvement ("this seems to be a lot better" —
user), but two things remained:

## Play-test fixes (round 5.1) — padding and rejecting off-map clicks

1. **Still some teleporting into walls, suspected to be a padding issue.** `IsCellOpen` was checking
   `Physics2D.OverlapCircle` at exactly the player's own collision radius — a "clear" cell was only
   guaranteed to not be *overlapping* a wall, not to have any real breathing room from one. Right up
   against a wall reads the same as "in the wall" once the player's sprite/body actually occupies that
   spot. Fix: `WalkableRegionSolver` now checks occupancy with `probeRadius + WallPadding` (a new
   constant, `0.1f`) instead of the bare radius, so accepted cells have a small buffer of clearance.
   This applies everywhere `IsCellOpen` is used (per-cell search *and* the final exact-click precision
   polish), so it can't be bypassed by landing exactly on the raw click.
2. **New requirement: clicking outside the play area (a wall, or off the ship) should do nothing**,
   rather than snapping to the nearest valid point regardless of distance. The user's framing was
   "the map is an overlay, walls are transparent, so only clicks that land in a room/hallway should
   count" — but rather than hit-testing the map's actual artwork/alpha (real feasibility unknown,
   texture may not be marked readable, would need new UV-mapping logic), this is achieved by reusing the
   reachability search's own output: `ApparaterMapButton` now measures the distance between the raw
   click and the nearest reachable point the solver found, and does nothing at all
   (`MaxSnapDistance = 1.5f`, no teleport) if that distance is too large. This falls out naturally: a
   click that's genuinely in a room/hallway needs at most a small nudge to reach real clearance; a click
   on a wall (especially with the added padding above making near-wall cells stricter) or in the void
   needs a much bigger jump back to actual floor, and gets rejected.

Both changes are tunable constants (`WallPadding` in `WalkableRegionSolver.cs`; `MaxSnapDistance` in
`ApparaterMapButton.cs`), not hardcoded assumptions — if clicks near legitimate large furniture (a big
meeting table) start getting incorrectly rejected as "outside the map," `MaxSnapDistance` is the first
thing to raise; if walls still don't feel like they have enough clearance, raise `WallPadding` (mind
corridor width — Skeld corridors are roughly 1.2–1.6 units, so keep `2 * (probeRadius + WallPadding)`
comfortably under that).

**Confirmed working by play-testing** (round 5.1 fixes included) — the reachability approach, wall
padding, and off-map-click rejection all hold up in live testing.

## Round 6 — simplification pass (no behavior change)

Feedback after round 5.1 confirmed correctness: "this is actually a lot better and seems to now be
working as intended. It does seem to be WAY overcomplicated though" — and asked whether a pregenerated
map of valid teleport points, generated from level bounding boxes, would be simpler.

Investigated via a design-focused subagent pass. **The literal idea doesn't hold up as a performance
win, and it's worth recording why so it isn't re-proposed without this context:**
- A *correct* precomputed map still needs the same per-edge wall-crossing check (`AnythingBetween`)
  baked in via a flood-fill — occupancy-only classification (just `OverlapCircle` per cell) cannot
  encode connectivity and would reintroduce the exact wall-tunneling bug from rounds 2–4. So a valid
  precomputed map is not cheaper-because-simpler classification; it's the same physics work, done
  eagerly instead of on demand.
- Eagerly covering enough area to answer *any* future click, before knowing where it'll land, costs
  **O((radius / cellSize)²)** — quadratically worse than the current goal-directed search's
  **O(distance / cellSize)** for a specific click, since a directed search only examines a thin path
  toward the target instead of a full disc in every direction.
- This ability is used roughly once per map-open (a successful click ends the effect immediately), so
  there's no repeated-lookup volume to amortize an eager precompute against — it would be pure overhead.

**A genuinely simpler alternative did surface**: a single swept `Physics2D.CircleCast` from the player
to the click (a "line-of-sight blink," ~10 lines, unlimited range, no grid or cache at all — the same
crossing-detection idea that fixed the original bug, just applied once over the whole distance instead
of per grid-cell edge). Its tradeoff is real and was presented to the user directly: it can't route
around corners like the current pathfinder does — it's a straight-line blink, not a walk. The user chose
to **keep the pathfinding/corner-routing behavior** over switching to this simpler mechanic.

**What was actually simplified**, given that choice (behavior-preserving, no logic change):
- `WalkableRegionSolver.cs`: replaced packed-`long` grid keys (`PackKey`/`UnpackKey`, bit-shifting a
  coordinate pair into a single `long`) with plain `(int Cx, int Cy)` tuple keys — .NET `ValueTuple`s
  already have structural equality/hashing, so this is a pure readability win with the packing helpers
  deleted entirely.
- `ApparaterMapButton.cs`: removed `LogMasksOnce`/`loggedMasksOnce` and `LogClickDiagnostics` (the
  mask-value dump and per-collider dump that existed specifically to diagnose *why* walls weren't being
  detected across rounds 2–5). That question is answered and the fix is confirmed working, so the
  diagnostic instrumentation was dead weight. The concise `Info(...)` calls for the three real outcomes
  (teleported / no reachable point / click too far from the play area) in `FixedUpdate` remain.

Explicitly **not** done, and shouldn't be revisited without a new, concrete reason: no eager/precomputed
grid, no session-scoped result caching (helps only the rare multi-click-per-open case, at real
complexity cost), no switch to line-of-sight `CircleCast` (the user's explicit choice).

## Known follow-ups

- Role icon (`Resources/RoleIcons/Apparater.png`) and ability button sprite
  (`Resources/CrewButtons/ApparaterMapButton.png`) are **placeholder art** (a simple generated purple
  portal-ring icon), not final assets — swap them out when real art exists.
- Not yet tested on multi-page maps (Polus/Airship/Fungle) or with a click on/near a closed door. By
  construction the search should be a non-regression here (it only ever expands within the player's own
  connected floor, seeded at their real position), but this hasn't been confirmed live.
- `WalkableRegionSolver`'s tunables (`MinCellSize`/`MaxCellSize` 0.15–0.25, `MaxExpandedCells` 8000,
  `WallPadding` 0.1) and `ApparaterMapButton`'s `MaxSnapDistance` (1.5) are reasoned first guesses, not
  exhaustively tuned against real play - if a snap feels too imprecise, too slow, fails on a
  legitimately-close click, or incorrectly rejects a click near large furniture, these are the values to
  revisit.
- `WalkableRegionSolver` is written as a general-purpose reusable utility (not Apparater-specific) —
  worth reaching for if a future role needs similar "place/move something to a valid nearby spot"
  logic, per the note in `docs/architecture.md`.
