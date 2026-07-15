# Apparater — development history

Round-by-round log of the play-test → bug-fix cycles that shaped the current teleport implementation.
This is archaeology, not a reference for day-to-day work — see [apparater.md](apparater.md) for how the
role actually works today. Read a round here when you need to know *why* something is built the way it
is, or want to avoid re-proposing an approach that was already tried and rejected.

## Round 1

The user tested the role in a live game and reported three bugs:

1. **Map showed tasks and vent icons.** `ShowNormalMap()` is a high-level call — TOU-Mira's own
   `ShowVentsPatch` Harmony postfix specifically targets `ShowNormalMap`/`ShowCountOverlay`/
   `ShowSabotageMap` to overlay vent icons, and `ShowNormalMap()` also leaves the task overlay visible.
   Fix: call `MapBehaviour.Instance.GenericShow()` instead (copying the pattern TOU-Mira's own
   `SpyAdminTableRoleButton` uses for its portable admin table), then manually configure overlay state.
   `GenericShow()` isn't a Harmony patch target, so the vent-icon postfix never runs.
2. **Long delay between clicking and teleporting.** Root cause: `Input.GetMouseButtonDown(0)` is a
   single-frame flag scoped to Unity's `Update()`, but was being read from `FixedUpdate()` — if no fixed
   tick lands on the exact render frame of the click, the down-event is silently dropped. MiraAPI's own
   `TeleportButton` example reads held-state (`GetKey`, never `GetKeyDown`) in `FixedUpdate` for this
   exact reason. Fix: poll `Input.GetMouseButton(0)` and do rising-edge detection manually with a
   `wasMouseDown` field (later replaced entirely — see round 10).
3. **Could teleport into obstacles** (engines, tables, admin console) — first attempt, later found still
   broken; see round 2. `Helpers.GetRoom()` only checks a room's broad polygon, not solid furniture
   colliders inside it.

## Round 2

Round 1's obstacle check (`Physics2D.OverlapPointAll` filtered to exclude trigger colliders and layers
5/8, copying `SentryPlaceCameraButton`/`MinerPlaceVentButton`) did not stop teleporting into walls or the
Electrical console. Root cause: that filter pattern doesn't fit this use case — Sentry/Miner run an
**unmasked** query at the player's own already-valid position, so they need that extra filtering to
avoid mistaking themselves/UI for an obstacle. Our query was already mask-scoped
(`Constants.ShipAndAllObjectsMask`); copying their filtering on top silently excluded the real obstacle
colliders via `!isTrigger`.

Fix: drop the filtering, trust the mask. Also switched from a zero-size point check to
`Physics2D.OverlapCircle` sized to the player's collider. Confirmed **partially** working: blocked
freestanding furniture correctly, but not walls or wall-embedded obstacles (Electrical, engines).

## Round 3

Researched whether anything in TOU-Mira/MiraAPI already solves "is an arbitrary point walkable" —
nothing does; every existing feature only validates the player's own current position or an
already-occupied position. Leading theory: ship walls (and wall-embedded obstacles) are thin boundary
colliders, not filled volumes — a point/circle test only registers a hit if the probe touches that line,
so a point just past a wall reads as clear.

Fix: added a reachability check using `PhysicsHelpers.AnythingBetween` (the same line-crossing primitive
TOU-Mira uses for vent/kill/placement checks) between the candidate and the room's AABB center. Also
added diagnostic logging. **Confirmed still broken** by play-testing: hallways redirected to the nearest
room (no `PlainShipRoom` entry for corridors), and walls/wall-embedded obstacles were unaffected.

## Round 4 — hit the limit of static analysis

Two research passes: (1) the sabotage map's clickable regions turned out to be prefab-authored for ~6-8
fixed rooms with fixed actions, not reusable per-room geometry; (2) inspected the installed game's
IL2CPP interop assembly via Mono.Cecil to read real mask values and `AnythingBetween`'s logic — dead end,
interop assemblies are marshaling stubs only, the actual logic is native/AOT and not inspectable from
disk. Conclusion: further certainty requires the game actually running, not more code reading.

Added `HasRoomMargin` (stricter — a ring of points around a candidate must also resolve to some room)
and much more diagnostic logging, then asked for real log output from four specific test clicks rather
than guessing again.

## Round 5 — architectural rewrite: reachability instead of point classification

Rounds 1-4 all asked "is this exact point valid?" — unanswerable for the failing cases, since a point
past a thin wall isn't inside any collider. The user reframed it: "can I actually walk there from here,
one short step at a time, without any step crossing a wall?" — a connectivity question a chain of
validated short steps can answer but isolated point testing cannot.

Confirmed via research: no existing flood-fill/BFS/pathfinding anywhere in either reference repo (this
is genuinely new code); the player's collider is a `CircleCollider2D`, ~0.4-0.5 world diameter.
Identified why round 2's `OverlapCircle` missed walls but round 3's `AnythingBetween` had no effect:
`AnythingBetween` *can* see thin walls, but round 3 drew one long, mis-anchored line that could pass
through a doorway gap and miss the actual wall segment. Many *short* segments between adjacent grid
cells is the reliable version of the same idea.

**New: `WalkableRegionSolver.TryFindReachablePoint`.** A greedy best-first search (ordered by squared
distance to the click) over a grid of ~0.15-0.25-unit cells, seeded at the player's real position and
expanding through 4-connected neighbors. A neighbor is accepted only if both an `OverlapCircle` at it
finds nothing, and `AnythingBetween` on the short step onto it finds nothing. Bounded by a hard
expansion cap; always returns the closest-to-click reachable cell seen. All the old point-classification
code (`TryFindNearestValidPoint`, `IsValidTeleportPoint`, `HasRoomMargin`, the room-center
`AnythingBetween` check) was deleted — `Helpers.GetRoom()` is no longer in the validation path at all,
which fixes hallways as a side effect (pure geometry has no "hallways aren't rooms" gap).

Play-tested as a real improvement, but two things remained — see round 5.1.

## Round 5.1 — padding and rejecting off-map clicks

1. **Still some teleporting into walls.** `IsCellOpen` checked occupancy at exactly the player's own
   collision radius — "clear" only meant "not overlapping," not "has breathing room." Fix: check
   occupancy at `probeRadius + WallPadding` (new constant, `0.1f`) everywhere `IsCellOpen` is used.
2. **New requirement: clicking outside the play area should do nothing**, not snap to the nearest valid
   point regardless of distance. Rather than hit-testing the map's actual artwork (unverified
   feasibility), this reuses the reachability search's own output: measure the distance between the raw
   click and the nearest reachable point found, and do nothing if it's too large (`MaxSnapDistance =
   1.5f`). A click genuinely in a room/hallway needs at most a small nudge; a click on a wall or in the
   void needs a much bigger jump back to real floor, and gets rejected.

**Confirmed working by play-testing** — the reachability approach, wall padding, and off-map-click
rejection all held up.

## Round 6 — simplification pass (no behavior change)

Feedback: "this is actually a lot better... It does seem to be WAY overcomplicated though" — asked
whether a pregenerated map of valid teleport points would be simpler.

Investigated via a design-focused subagent pass; the idea doesn't hold up as a performance win, worth
recording why so it isn't re-proposed:
- A *correct* precomputed map still needs the same per-edge wall-crossing check baked in via a
  flood-fill — occupancy-only classification would reintroduce the exact wall-tunneling bug from rounds
  2-4. It's the same physics work, done eagerly instead of on demand.
- Eagerly covering enough area to answer *any* future click costs **O((radius/cellSize)²)** —
  quadratically worse than the current goal-directed search's **O(distance/cellSize)** for one specific
  click.
- The ability is used roughly once per map-open, so there's no repeated-lookup volume to amortize an
  eager precompute against.

A genuinely simpler alternative did surface: a single swept `Physics2D.CircleCast` from player to click
("line-of-sight blink," unlimited range, no grid at all) — but it can't route around corners. Presented
to the user directly; **they chose to keep the pathfinding/corner-routing behavior.**

What was actually simplified (behavior-preserving): replaced packed-`long` grid keys with plain
`(int, int)` tuple keys in `WalkableRegionSolver`; removed the mask-dump/per-collider diagnostic logging
in `ApparaterMapButton` (its job was done — the wall-detection question was answered).

## Round 7 — polish pass

Follow-up requests, all applied together:
- Color scheme purple → green (`SuperSquadColors.Apparater`).
- Default cooldown 30s → 6s (option range moved to match).
- **Use-not-consumed-unless-teleported**, first attempt: MiraAPI's `ClickHandler()` deducts a use the
  instant the button is pressed, before `OnClick()` runs — so opening the map always "spent" a use even
  if the player never teleported. Fix: a `teleported` flag, refunded in `OnEffectEnd()` if false.
- Ability renamed "Teleport" → "Aparate" (later corrected to "Apparate").
- `MaxUses` given an "infinite" setting — **this attempt was actually broken**, see round 8 item 4.

## Round 8

Follow-up play-test feedback:

1. **"Cooldown still 30s by default in practice mode."** No code bug — Among Us's host-side option
   presets persist to disk independently of the mod's compiled defaults; a preset slot already touched
   under the old `30f` default keeps its saved value until manually changed or reset.
2. **"Still doing the countdown; map shouldn't close unless the player teleports or closes it."** The
   `SelectTime` option was a max-time-to-choose timer; once it hit 0 the base class auto-fired
   `OnEffectEnd()` and force-closed the map. Removed `SelectTime` entirely; `ApparaterMapButton` set
   `HasEffect => true` while leaving `EffectDuration` at 0 — **this broke things further, see round 9.**
3. **"Closing the map or running out of time resets the cooldown — it shouldn't."** Fixed alongside the
   use-refund: `OnEffectEnd()` now sets `Timer = 0f` when `!teleported`, instead of leaving the
   already-applied `Timer = Cooldown` in place.
4. **The round-7 "infinite uses" setting was actually broken.** `zeroInfinity: true` on
   `[ModdedNumberOption]` only controls what the options-menu slider *displays* at 0 — it has no
   connection to `CustomActionButton.ZeroIsInfinite` (an unrelated, confusingly-similarly-named bool on
   the *button*). `TownOfUsButton` (which this button inherits from) hardcodes `ZeroIsInfinite = false`,
   so under a 0-15 range, "infinite" actually meant `UsesLeft` stuck at 0 forever — permanently unusable.
   Fixed by following `EngineerOptions`' own convention for the same button hierarchy: `-1` is the
   infinite sentinel, not `0`.
5. **Map marker color: switched from the player's own color to plain crewmate blue** (`Palette
   .CrewmateBlue`), matching what the user asked for at the time (later revised again — see rounds 9-10).

## Round 9 — round 8's fix broke the map (auto-closed in ~2 ticks)

Play-test: the map opened for only a few rendered frames before closing itself; background was still
red; the marker flashed green instead of the player's real color. The user correctly guessed the shape
of the bug before any code was reread.

**Bug 1: the auto-close.** `ApparaterMapButton` inherits through `TownOfUsRoleButton<T>` →
`TownOfUsButton`, and `TownOfUsButton` **overrides** `FixedUpdateHandler` with its own auto-end
condition that has no `EffectDuration > 0` guard at all (unlike the base `CustomActionButton` version
round 8's reasoning was based on). With `EffectDuration` at 0, `Timer` went negative on the very next
tick and fired the unconditional auto-end almost immediately. Fix: `EffectDuration` now returns a
constant (`TimerKeepAlive = 5f`), and `FixedUpdate` re-arms `Timer = TimerKeepAlive` every tick so it
never runs out. The countdown text is force-hidden each tick (after the base draw call already ran) so
it never visibly renders.

**Bug 2: map background still red.** `MapBehaviour.ColorControl` (an `AlphaPulse`) is what
`ShowNormalMap()`/`ShowSabotageMap()` presumably set as a side effect of their internal mode — but
`GenericShow()` skips that, leaving whatever color it last happened to be. Fix: set it explicitly,
`map.ColorControl.SetColor(tint)`.

**Bug 3: marker flashed green.** No separate bug found — most likely a downstream symptom of bug 1 (a
map that barely survives 1-2 physics ticks produces confusing mid-transition rendering).

**Refactor alongside the fix**, motivated by the user's own follow-up idea (a possible future impostor
map-ability, e.g. click-to-airstrike — explicitly deferred, "that's for later"): pulled the map-opening
logic out of `ApparaterMapButton` into `Modules/BareMapVisuals.cs` (`Open(Color tint)` / `Close()`), so a
future impostor ability can reuse it with a red tint instead of duplicating it later.

## Round 10 — clicks mostly dropped (the real click bug), plus map colors done properly

Play-test: the ability counter/cooldown behaved, but most map clicks did nothing — only very
inconsistently did one register.

**Bug 1 (the big one).** Click detection had always polled `Input.GetMouseButton(0)` in `FixedUpdate`
with manual rising-edge detection. `FixedUpdate` runs on a fixed timestep (~50 Hz), not once per
rendered frame — a quick click whose pressed-state lasts less than one fixed-timestep interval is never
sampled and is silently dropped. Whether any given click lands on a fixed tick is essentially random.

Fix: read the click from an **Update** context instead. New Harmony patch `ApparaterMapClickPatch` — a
postfix on `HudManager.Update` (runs every rendered frame) reads the single-frame
`GetMouseButtonDown(0)` (reliable here, since Update is the frame that sets it) and forwards to a new
public `HandleMapClick()` on the button. All FixedUpdate mouse-polling was removed.

**Bug 2: marker was green, not the player's own color.** Round 9 had set `HerePoint.color = tint`
directly — `HerePoint` renders through the player-cosmetic material pipeline, and a plain `.color =`
assignment bypasses that. Reverted to `SetPlayerMaterialColors(HerePoint)`, matching TOU-Mira's own
portable-admin buttons.

**Bug 3: map background was cyan-ish, not the normal task-map blue.** Round 9 used `Palette.CrewmateBlue`
— a UI/text team color, not the map background color. TOU-Mira's own `MapBehaviourPatch.ShowNormalMap`
postfix shows the real constant: `Palette.Blue`. Switched to that.

**Bug 4: vents showing.** `GenericShow()` doesn't *create* vent icons, but TOU-Mira's `ShowVentsPatch`
stores the icons it makes for the vanilla map in static dicts that are only torn down at round start —
never on map close. Opening the vanilla task map even once (easy in practice mode) leaves those icons
sitting under the map root, and they reappear the moment `GenericShow()` is called. `BareMapVisuals.Open`
now destroys those leftover icons.

`BareMapVisuals.Open` was generalized to take the background color as a parameter for the same
future-impostor-ability reason as round 9.

## Round 11 — origin lockout: some standing spots made every click fail

Play-test: from certain standing spots (hugging a wall, standing in a corner), every map click was
silently ignored, and moving slightly instantly fixed it — a function of *where the player stood*, not
where they clicked.

Root cause: `WallPadding` was applied to **every** cell's occupancy check, including cells adjacent to
the player's own position. The game's movement lets a player stand closer to a wall (~collider-radius)
than the padded clearance requires. In a 90° corner, all four cardinal neighbors of the seed cell failed
the padded check, the frontier emptied on the first iteration, and the solver returned false for every
click. The same logic also made corridors narrower than the padded diameter silently unpathable.

Fix — split traversal from landing: traversal occupancy checked at `probeRadius * TraversalRadiusFactor`
(0.9×, since the per-edge `AnythingBetween` check is the real anti-wall-crossing guard, not the occupancy
probe); only the *landing* spot requires the padded radius. Grid went 4-connected → 8-connected so a
corner has a diagonal escape. Clicks within one cell of the player now short-circuit immediately.

## Round 12 — origin lockout persisted; rearchitected to a dual seed

Round 11's fix did **not** resolve the wall-pressed lockout in live testing, even though the diagnosis
(a function of standing position) was confirmed correct — the exact micro-mechanism was never fully
pinned from static analysis alone. Rather than iterate blind on a fourth micro-fix, the user proposed a
different angle: stop depending on the player's surroundings at all, and use a "universal safe point"
instead — flagging (correctly, as it turned out) that the meeting spawn *center* is likely obstructed by
the emergency-button table.

**Fix: a dual seed.** The solver now seeds the search both at the player's position, *and* at a
pre-validated "anchor" sampled from the map's own spawn ring (`TryFindSpawnAnchor` — 16 points around
`ShipStatus.MeetingSpawnCenter`, falling back to `InitialSpawnCenter`/`MeetingSpawnCenter2`, with each
circle's center tried only as a last resort, since it's usually the obstructed spot). Verified the
`ShipStatus` member names/types against the installed game's interop assembly via a Cecil dump. When the
player seed is dead, the anchor alone drives the search — origin lockout becomes impossible by
construction, whatever its exact cause.

Deliberate semantic consequence: destination validity now means "connected to the playable area" rather
than "walkable from the player right now" — teleporting past a closed door now works. This matched what
the user wanted when asked directly.

## Round 13 — found the actual root cause: the wrong `AnythingBetween` overload

Round 12 made lockout *survivable* but didn't explain the specific pattern the user then reported:
teleport still failed whenever the player's real body overlapped or was embedded in solid geometry (a
corner, Storage's center obstacle, touching the engine sprite) — regardless of where on the map the
click or the anchor was. The user asked directly: "are you sure we aren't still using the player
position?" — correct, just not in the seed.

Root cause, found via a precedent-usage research pass: `HasClearEdge` called
`PhysicsHelpers.AnythingBetween(selfCollider, from, to, mask, false)` — the **collider-based** overload,
always passing the player's own live `Collider2D`, for every edge check regardless of seed. Unity's
collider-based cast overloads sweep the passed collider's *own current shape from its real, live
Transform position* — `source`/`target` only derive a direction and distance, they don't relocate the
collider. A sweep that begins already overlapping something reports a hit immediately, in every
direction — so every edge check anywhere on the map was secretly gated by whatever the player's real
body currently touched.

Confirmed via precedent: every other call site of the collider overload in `reference/TOU-Mira`/
`reference/MiraAPI` always passes `source` equal to that same collider's own real position — none of
them call it with positions displaced from the passed collider, because their use case is always "is
there a wall between me and this thing near me." This solver was the first caller to break that pattern.
TOU-Mira's own `GhostRolePatches.cs`, by contrast, uses the **collider-less** overload —
`AnythingBetween(Vector2, Vector2, int, bool)` — for line-of-sight between two arbitrary positions with
no collider involved. That's the correct precedent.

Fix: switched `HasClearEdge` to the collider-less overload, dropping the `Collider2D` parameter from
`TryFindReachablePoint` entirely. Every edge check is now a pure geometric query between the two grid
positions being tested, with zero dependency on the player's real-world contact state.
