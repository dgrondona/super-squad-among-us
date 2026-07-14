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
3. **Could teleport into obstacles** (engines, tables, admin console, etc.). `Helpers.GetRoom()` only
   checks a room's broad polygon, not solid furniture/console colliders inside it — a click that's
   "in the engine room" per that polygon can still be on top of the solid engine console collider. Fix:
   `IsObstructed` checks `Physics2D.OverlapPointAll(point, Constants.ShipAndAllObjectsMask)` for any
   non-trigger collider (excluding the Players and IgnoreRaycast layers), the same mask/filter pattern
   TOU-Mira uses for placement validation in `SentryPlaceCameraButton`/`MinerPlaceVentButton`. If the
   raw clicked point is obstructed (or outside any room), `TryFindNearestValidPoint` searches outward in
   expanding rings (0.2 unit steps, 16 angles per ring, up to 4 units) for the nearest point that's both
   inside a room and unobstructed, and teleports there instead. If nothing valid is found within that
   radius, the click is silently ignored (safer than teleporting somewhere nonsensical) and the player
   can just click again.

## Known follow-ups

- Role icon (`Resources/RoleIcons/Apparater.png`) and ability button sprite
  (`Resources/CrewButtons/ApparaterMapButton.png`) are **placeholder art** (a simple generated purple
  portal-ring icon), not final assets — swap them out when real art exists.
- The nearest-valid-point search (ring-scan, see above) hasn't itself been play-tested yet — the
  underlying obstacle/room checks are confirmed-good primitives from elsewhere in TOU-Mira, but the
  search parameters (0.2 step, 4 unit max, 16 angles) are an untested first guess. If it ever picks a
  spot that feels too far from the actual click, or fails to find a spot when one clearly exists nearby,
  those constants at the top of `ApparaterMapButton.cs` are the place to tune.
- Not yet tested on multi-page maps (Polus/Airship/Fungle) or with a click on/near a closed door.
