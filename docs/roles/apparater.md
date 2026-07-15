# Apparater

Crewmate Power role. Opens the ship map as a target-picker; clicking teleports the player to the
nearest reachable point.

Files: `Roles/Crewmate/ApparaterRole.cs`, `Buttons/Crewmate/ApparaterMapButton.cs`,
`Options/Roles/Crewmate/ApparaterOptions.cs`, `Modules/WalkableRegionSolver.cs`,
`Modules/BareMapVisuals.cs`, `Patches/ApparaterMapClickPatch.cs`.

Thirteen rounds of play-test/fix cycles got the teleport from "doesn't work" to the design below —
that's archived in [apparater-history.md](apparater-history.md). Read it when you need to know *why*
something here looks the way it does, or before proposing an approach that sounds simpler (a few already
were tried and rejected with good reason — see "Design decisions" below).

## Design decisions (confirmed with the user)

- **Movement while the map is open: allowed.** The player can keep walking while picking a destination.
- **Map screen: bare minimap** — ship layout + the player's own position dot only, no tasks/vents/counts.
- **Destination validity means "connected to the playable area," not "walkable from the player right
  now."** Teleporting past a closed door works, because the search is seeded independently of the player
  (see `WalkableRegionSolver` below). Deliberate, confirmed with the user.
- **Kept the corner-routing pathfinder over a simpler line-of-sight blink.** A single swept `CircleCast`
  ("blink to wherever you can see, unlimited range, no grid") was offered as a much simpler alternative;
  the user chose to keep routing around obstacles instead. Don't propose switching without a new reason.
- **A pregenerated map of valid teleport points was investigated and rejected** — it needs the same
  per-edge wall-crossing work as the current search, just done eagerly for every direction instead of
  toward one click, which is strictly more expensive for how this ability is actually used (see history,
  round 6, for the complexity argument).
- **`BareMapVisuals`'s background tint is a parameter, not hardcoded**, so a possible future impostor
  map-ability (the user's idea: click-to-airstrike) can reuse it with `Palette.ImpostorRed`. Not built.

## How it works

### Ability lifecycle (`ApparaterMapButton`)

Click → `BareMapVisuals.Open(Palette.Blue)` shows the bare map. It then stays open indefinitely — no
selection-timer countdown — until the player clicks a destination or closes the map themselves. Either
way costs nothing extra: a use is only spent, and the cooldown only applied, when an actual teleport
happens (tracked by a `teleported` flag, checked in `OnEffectEnd`). Closing without teleporting refunds
the use `ClickHandler` optimistically deducted on open, and zeroes the cooldown timer.

Load-bearing quirk: on this button's inheritance chain (`TownOfUsRoleButton<T>` → `TownOfUsButton`),
`EffectDuration` **cannot be `0`** — `TownOfUsButton`'s own `FixedUpdateHandler` override has no
`EffectDuration > 0` guard on its auto-end path, so a `0` duration force-closes the map almost
immediately. `TimerKeepAlive` (a small positive constant) is used instead, and `FixedUpdate` re-arms
`Timer = TimerKeepAlive` every tick so it never actually counts down.

### Click detection (`ApparaterMapClickPatch`)

The click itself is detected from a Harmony postfix on `HudManager.Update`, not by polling the mouse in
the button's own `FixedUpdate`. `FixedUpdate` runs on a fixed timestep, not once per rendered frame, so
a quick click can start and end between two ticks and never be sampled — confirmed as the actual cause
of "clicks mostly don't register" in testing, not a theoretical concern. The patch reads
`Input.GetMouseButtonDown(0)` (reliable here, since `Update` is the frame that sets that flag) and
forwards to `ApparaterMapButton.HandleMapClick()`, which no-ops unless this player's map is open.

### Coordinate conversion

Screen click → world position: `Camera.main.ScreenToWorldPoint` → `InverseTransformPoint` relative to
the map root → `* ShipStatus.Instance.MapScale` — the inverse of the formula TOU-Mira uses to place
vent/body icons on the map (`worldPos / MapScale`). Confirmed correct on the maps tested so far; not
separately confirmed on multi-page maps (Polus/Airship/Fungle's "swipe" map).

### Reachability search (`WalkableRegionSolver`)

The problem: a point on the far side of a thin wall collider (or embedded in a wall-attached obstacle)
isn't *inside* any collider, so no point/circle overlap test alone can ever see it as blocked — only a
chain of validated short steps from a known-good position can guarantee a destination is reachable
without tunneling through a wall. `TryFindReachablePoint` is a greedy best-first search (ordered by
distance to the click) over 8-connected grid cells, seeded twice:

1. **The player's own position.**
2. **A spawn-ring anchor** — a pre-validated clear point sampled from the map's own
   `ShipStatus.MeetingSpawnCenter` ring (falling back to `InitialSpawnCenter`, then
   `MeetingSpawnCenter2`, then each circle's center as a last resort — the center is usually obstructed
   by the meeting table). This exists because the player's own position alone was, in practice, an
   unreliable search seed in some standing spots; the anchor makes the search's correctness independent
   of where the player is standing or what they're touching.

A cell is accepted into the search only if both:
- **Traversal**: `Physics2D.OverlapCircle` at `probeRadius * TraversalRadiusFactor` (slightly under the
  body's true radius) finds nothing.
- **Edge**: `PhysicsHelpers.AnythingBetween(from, to, mask, false)` — the **collider-less** overload,
  deliberately — finds nothing on the short step onto it. Do not pass a `Collider2D` here: that overload
  sweeps *that collider's own live position*, not the `from`/`to` positions given to it, which silently
  makes every edge check depend on whatever body the collider belongs to is currently touching rather
  than the two points actually being tested (see history, round 13).

Only a cell that also passes a stricter, *padded* check (`probeRadius + WallPadding`) is eligible to be
the final landing spot, so the destination always has some clearance — deliberately **not** required for
cells merely traveled through, since requiring it everywhere made narrow-but-walkable corridors
unpathable and could strand the search when the player was already standing close to geometry.

Bounded by `MaxExpandedCells` so a click clear across the ship can't cause a hitch; always returns the
closest-to-click reachable point seen, even if the cap is hit. `ApparaterMapButton` rejects the result
entirely (no teleport at all) if it's more than `MaxSnapDistance` from the raw click — this is what
makes clicking on a wall or off the ship a no-op instead of snapping to the nearest room regardless of
distance.

### Map visuals (`BareMapVisuals`)

`GenericShow()` (not `ShowNormalMap()`) shows the bare ship layout without triggering TOU-Mira's
vent-icon Harmony postfix or leaving the task overlay visible — but it also skips the color/marker side
effects those other `Show*` methods have, so `BareMapVisuals.Open` sets them explicitly:
- `HerePoint` marker via `SetPlayerMaterialColors` — not a plain `.color =`, which bypasses the
  player-cosmetic material pipeline and renders the wrong color.
- Background via `ColorControl.SetColor(backgroundColor)` — `Palette.Blue` for Apparater, matching the
  vanilla task map; passed as a parameter so a future impostor ability can pass `Palette.ImpostorRed`.
- Clears any vent/body icons left over from a previous `ShowNormalMap`/`ShowSabotageMap` open elsewhere
  in the session — those are only torn down at round start, never on map close, by the patch that
  creates them, so they'd otherwise still be parented under the map root and reappear here too.

## Known follow-ups

- Role icon and ability sprite are placeholder art (generated, not final) — swap out when real art
  exists.
- Not tested on multi-page maps (Polus/Airship/Fungle) or maps with sections connected only by
  ladders/moving platforms (Airship) — the search can't grid-walk across a gap like that from either
  seed, so cross-section teleports there will likely be rejected until it's specifically addressed (e.g.
  one anchor per ladder-connected section).
- `WalkableRegionSolver`'s tunables (`MinCellSize`/`MaxCellSize`, `MaxExpandedCells`, `WallPadding`,
  `TraversalRadiusFactor`) and `ApparaterMapButton.MaxSnapDistance` are reasoned first guesses, not
  exhaustively tuned against real play.
- `WalkableRegionSolver` is written as a general-purpose reusable utility, not Apparater-specific —
  reach for it before writing another point-classification check for a similar "place/move something to
  a valid nearby spot" problem (see `docs/architecture.md`).
