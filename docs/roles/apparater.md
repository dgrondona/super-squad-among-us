# Apparater

Crewmate Power role. Opens the ship map as a target-picker; clicking teleports the player to the
nearest reachable point.

Files: `Roles/Crewmate/ApparaterRole.cs`, `Buttons/Crewmate/ApparaterMapButton.cs`,
`Options/Roles/Crewmate/ApparaterOptions.cs`, `Modules/WalkableRegionSolver.cs`,
`Modules/MiniMapMask.cs`, `Modules/BareMapVisuals.cs`, `Patches/ApparaterMapClickPatch.cs`.

Thirteen rounds of play-test/fix cycles got the teleport from "doesn't work" to the design below —
that's archived in [apparater-history.md](apparater-history.md). Read it when you need to know *why*
something here looks the way it does, or before proposing an approach that sounds simpler (a few already
were tried and rejected with good reason — see "Design decisions" below).

## Design decisions (confirmed with the user)

- **Movement while the map is open: allowed.** The player can keep walking while picking a destination.
- **Map screen: bare minimap** — ship layout + the player's own position dot only, no tasks/vents/counts.
- **Destination validity means "connected to the playable area," not "walkable from the player right
  now."** Teleporting past a closed door works **in both directions**, because the search is seeded
  independently of the player *and* traverses blind to door colliders (see `WalkableRegionSolver`
  below). Deliberate, confirmed with the user.
  - This bullet used to claim closed doors worked while only the *outbound* case had ever been tried.
    They didn't: a closed door is a solid collider, and both seeds lived in the main map component, so
    sealed-room → map succeeded while map → sealed-room failed. Fixed in round 14.
- **Connectivity is load-bearing for obstacle avoidance, not just for "is this on the ship."** Obstacle
  colliders are hollow *outlines* — Skeld's ground geometry is `EdgeCollider2D` polylines — so the space
  inside the Storage crates or an engine overlaps no collider and a point test reports it as open. Only
  reachability excludes those pockets. **Do not replace the search with a "snap to the nearest open
  point" scan**; it was proposed in round 14 and would teleport the player inside the crates.
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

### Click detection and validation (`ApparaterMapClickPatch` + `MiniMapMask`)

The click itself is detected from a Harmony postfix on `HudManager.Update`, not by polling the mouse in
the button's own `FixedUpdate`. `FixedUpdate` runs on a fixed timestep, not once per rendered frame, so
a quick click can start and end between two ticks and never be sampled — confirmed as the actual cause
of "clicks mostly don't register" in testing, not a theoretical concern. The patch reads
`Input.GetMouseButtonDown(0)` (reliable here, since `Update` is the frame that sets that flag) and
forwards to `ApparaterMapButton.HandleMapClick()`, which no-ops unless this player's map is open.

**Playtest bug (2026-07-16)**: clicking large in-room obstacles (the crate pile in Storage's center)
did nothing — the nearest reachable point was farther than the old 1.5u `MaxSnapDistance` cap, which
existed only to stop wall/off-map clicks from snapping into rooms.

**Click validity: minimap texture alpha.** Rather than geometric probing, `MiniMapMask` samples the
minimap's own background sprite texture (via `MapBehaviour.ColorControl.rend`): the vanilla map texture
is fully transparent (alpha ≈ 0) over walls and off-map, and semi-transparent over rooms and halls. A
click's validity is literally `textureAlpha >= RoomAlphaThreshold` (0.05). This is the "is this on the
ship" answer directly from the art asset, no collision-detection guesswork.

**Sampling internals.** World click → sprite-local via `renderer.transform.InverseTransformPoint` →
pixels via `sprite.rect.x/y + sprite.pivot + local * pixelsPerUnit`. Note: runtime `Sprite.pivot` is
in PIXELS (the normalized pivot only exists on importer settings); an agent-suggested "correction" to
normalized was tried and rejected. Points outside the sprite rect are classified as off-map. Game
textures aren't CPU-readable, so a readable copy is blit once per texture (RenderTexture → ReadPixels)
and cached by `GetInstanceID()`, rebuilt when the map changes between games. The `ColorControl.SetColor`
tint in `BareMapVisuals` doesn't affect texture pixels, so recolors don't invalidate the cache.

**Snap-distance generosity.** Alpha-confirmed clicks use `InRoomSnapDistance = 6f` (generous) so
clicking a large in-room obstacle snaps to the surrounding walkable floor. Alpha-rejected (wall/off-map)
clicks are ignored outright, never snapped. Only when the texture can't be sampled (no sprite, no map,
copy failure) does the fallback `MaxSnapDistance = 1.5f` geometry-only path run — the same tight cap
that used to apply to all clicks. Also added the same `Camera.main == null` guard the Sniper got.

### Coordinate conversion

Screen click → world position: `Camera.main.ScreenToWorldPoint` → `InverseTransformPoint` relative to
the map root → `* ShipStatus.Instance.MapScale` → `x *= Mathf.Sign(ShipStatus.transform.localScale.x)`
— the inverse of the formula TOU-Mira uses to place vent/body icons on the map (`worldPos / MapScale`).
Confirmed correct on the maps tested so far; not separately confirmed on multi-page maps
(Polus/Airship/Fungle's "swipe" map).

The sign flip handles mirrored maps (Dleks), which reuse Skeld's *un-mirrored* map sprite and flip the
ship instead. Prior art all converts world→map-local, but a sign flip is its own inverse so the same
multiply applies in our direction.

**Two different transforms, deliberately.** `MiniMapMask` samples alpha against `ColorControl.rend`'s
transform (the map's `Background` object) while `GetRawClickWorldPosition` converts relative to
`HerePoint.transform.parent` (`HereIndicatorParent`). These are different objects, separated by a per-map
translation (Skeld `(0.54, 1.25)`, Polus `(-4.15, 2.45)`). It looks like a bug and isn't: `HereIndicator
Parent` is the frame where map-local × `MapScale` equals ship-world (which is why every mod parents map
icons there), and `Background` is the frame the sprite's pixels live in. That offset *is* the
pivot-vs-ship-origin correction, and correctly isn't applied to the pixel lookup. Investigated and
dismissed twice — don't open it a third time. It's also why the Dleks sign flip belongs only on the
ship-space conversion, not on the alpha sample.

### Reachability search (`WalkableRegionSolver`)

The problem: a point on the far side of a thin wall collider (or embedded in a wall-attached obstacle)
isn't *inside* any collider, so no point/circle overlap test alone can ever see it as blocked — only a
chain of validated short steps from a known-good position can guarantee a destination is reachable
without tunneling through a wall. `TryFindReachablePoint` is a greedy best-first search (ordered by
distance to the click) over 8-connected grid cells, seeded from:

1. **The player's own position.**
2. **Every known-good standing position on the map** (`CollectSeedPoints`), so a region the player can't
   walk to is still searchable. Each is landing-validated before use, so a bad candidate is silently
   dropped rather than trusted:
   - the spawn **rings** around `MeetingSpawnCenter`/`InitialSpawnCenter`/`MeetingSpawnCenter2` — vanilla
     places players *on* the ring at `SpawnRadius`, never at the centre, which is the meeting table (the
     old bare-centre last-resort fallback was removed in round 14 for exactly that reason);
   - every **vent** (`AllVents`, `+0.3636f` on Y — TOU-Mira's own stand-in-front offset), which are
     authored standing positions and therefore valid by construction;
   - **link endpoints** — ladders and their `Destination`, Airship's `GapPlatform.Left/RightUsePosition`,
     Fungle's `Zipline.landingPositionTop/Bottom` — covering sections a grid walk can't cross.

This makes the search's correctness independent of where the player is standing or what they're touching.

A cell is accepted into the search only if both:
- **Traversal**: `Physics2D.OverlapCircle` at `probeRadius * TraversalRadiusFactor` (slightly under the
  body's true radius) finds nothing **but doors**.
- **Edge**: a positional `Physics2D.Linecast` finds nothing **but doors** on the short step onto it. Use
  the multi-result overload — the single-hit form returns only the *closest* collider, so it would clear
  an edge whenever the nearest thing on it is a door with a wall just behind (real geometry at a door
  jamb). And never a collider-based cast: those sweep *that collider's own live position*, not the
  `from`/`to` given to them, which silently makes every edge check depend on whatever the collider's body
  is currently touching (see history, round 13).

**Doors are ignored during traversal, respected when landing.** A closed door is a solid collider and an
open one is a trigger, so an unfiltered `useTriggers = false` query treats a sealed room as walled off in
both directions. `CollectDoorColliderIds` gathers every door's blocking collider and the two probes above
skip them; the landing tier does not, so you can path *through* a closed door but not stop inside one.
Door state is only ever **read** — an earlier draft flipped `isTrigger` during the search and was rejected,
since mutating shared collider state could let a player pressed against a door slip through, and it raises
sabotage/desync questions that filtering never has to answer.

Both probes **fail closed on a saturated buffer**: the buffer overloads fill to the array length and
report that count with no signal that more colliders overlapped, so a real wall outside the truncated
window would be invisible and wrongly read as open.

Only a cell that also passes a stricter, *padded* check (`probeRadius + WallPadding`) is eligible to be
the final landing spot, so the destination always has some clearance — deliberately **not** required for
cells merely traveled through, since requiring it everywhere made narrow-but-walkable corridors
unpathable and could strand the search when the player was already standing close to geometry.

**Padding note (2026-07-16):** landing-point safety is UNCHANGED — `WalkableRegionSolver` still validates
the destination with the player's `probeRadius` plus `WallPadding` (0.1u), so the teleport can't put the
player inside or against a wall. The alpha mask only decides click validity; `WalkableRegionSolver` ensures
safe landing.

Bounded by `MaxExpandedCells` so a click clear across the ship can't cause a hitch; always returns the
closest-to-click reachable point seen, even if the cap is hit. `ApparaterMapButton` rejects the result
entirely (no teleport at all) if it's more than the configured snap cap from the raw click. With alpha
confirmation, the cap is generous (`InRoomSnapDistance = 6f`) to handle large obstacles; with alpha
unavailable, the tight fallback cap (`MaxSnapDistance = 1.5f`) prevents snapping to unintended distant
rooms.

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
- **Not tested on multi-page maps** (Polus/Airship/Fungle's "swipe" map). This is a *coordinate
  conversion* concern and is still open — round 14's per-map click matrix retires it only if each map's
  page-swipe UI is explicitly exercised. (The separate ladder/platform limitation this bullet used to
  carry was fixed in round 14 by seeding link endpoints.)
- **`SensorDoor` is unresolved.** A vanilla `MonoBehaviour` that isn't a `SomeKindaDoor` and exposes no
  collider in its signature; MIRA HQ's sliding doors are the leading candidate. If it blocks movement
  through `ShipAndAllObjectsMask` it is invisible to `CollectDoorColliderIds`, exactly like
  `AutoOpenMushroomDoor` nearly was. Confirm on MIRA HQ with doors closed.
- `WalkableRegionSolver`'s tunables (`MinCellSize`/`MaxCellSize`, `MaxExpandedCells`, `WallPadding`,
  `TraversalRadiusFactor`, `HitBufferSize`) and `ApparaterMapButton.MaxSnapDistance` are reasoned first
  guesses, not exhaustively tuned against real play.
- **Search cost is now logged, not bounded by measurement.** `ApparaterMapButton` reports search
  milliseconds on every click. Door filtering made each probe slightly dearer and round 14 added seeds,
  so watch for a hitch. If one appears: order seeds by distance to the click first (nearly free — the
  frontier is already distance-keyed), and only then consider a bidirectional search (flood from the
  click and the seed side, stop when they meet). Don't build the latter speculatively.
- `WalkableRegionSolver` is written as a general-purpose reusable utility, not Apparater-specific —
  reach for it before writing another point-classification check for a similar "place/move something to
  a valid nearby spot" problem (see `docs/architecture.md`).
