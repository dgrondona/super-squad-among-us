# Invisible Boy

Crewmate Support role. Passive: automatically invisible whenever no living player has line of sight to
him; visible the instant anyone can see even a sliver of him.

Files: `Roles/Crewmate/InvisibleBoyRole.cs`, `Modifiers/InvisibleBoyModifier.cs`,
`Modules/SightChecker.cs`, `Patches/InvisibleBoyAdminPatch.cs`.

## Design decisions (confirmed with the user)

- **Watchers are all living players** — including players currently in a vent, and including other
  concealed/invisible players. Dead players and spectators never count as watchers. Cameras and the
  admin table are not watchers either (they don't grant sight for the purposes of *this* role's own
  visibility check, even though the role is separately hidden from them — see below).
- **"Even a sliver visible → visible."** Visibility is decided by multi-point body sampling, not a
  single center-point check — if any sampled point on Invisible Boy's body is seen by any watcher, he's
  fully visible, not partially.
- **Reveal is instant; conceal fades in after a grace period.** Becoming visible happens the moment
  he's seen, no delay. Going invisible only happens after a short (0.5s) continuous "unseen" grace
  window, to avoid flicker when sight lines cross him briefly (e.g. a watcher's rotation or a momentary
  obstruction). This grace is a fixed anti-flicker constant, not a tunable lobby option.
- **Zero lobby options, by design.** No cooldowns, no toggles, nothing in `Options/` for this role — the
  passive is always-on and non-configurable.

## How it works

### Own-client visibility loop

The role's own-client `FixedUpdate` loop (not a networked authority check) repeatedly evaluates whether
any living player currently has sight of him, and toggles a networked `InvisibleBoyModifier` accordingly
so all clients render the state consistently. Rendering reuses `TownOfUsAppearances.Swooper`-style
handling: fully `Color.clear` to other players, and a ghostly ~0.1-alpha self-view so the Invisible Boy
player can still see their own outline. Reusing the existing Swooper appearance path means this doesn't
fight with comms-camouflage-style sabotages that already hook into that same rendering path.

### Sight determination (`SightChecker`)

For each living player (the "watcher"), sight is computed as:

1. **Range** — the watcher's own `CalculateLightRadius()`, which already accounts for impostor vision
   and the lights sabotage, so an impostor or a lights-out crew member naturally sees further/less.
2. **Occlusion** — a collider-less `PhysicsHelpers.AnythingBetween` linecast from the watcher to each
   sampled body point on Invisible Boy, masked against `Constants.ShadowMask` (the lighting-occlusion
   layer) — deliberately **not** `Constants.ShipAndObjectsMask`, which also includes see-over furniture
   that shouldn't block sight the way a wall does.

If any watcher is within range and has an unoccluded line to any sampled point, Invisible Boy is
visible.

### Admin table (`InvisibleBoyAdminPatch`)

The admin table is hidden via a Harmony prefix/postfix pair on `MapCountOverlay.Update` that disables
the invisible player's colliders only for the duration of that count, then restores exactly what it
disabled (not a blanket re-enable, in case something else legitimately had them off). Patch priority is
`Priority.High` so this runs *before* TOU-Mira's own Spy-role prefix on the same method — ordering
matters here since Spy's logic also inspects collider state during the count.

### Cameras

Cameras need no dedicated patch — because the player's renderer is already set fully transparent
(`Color.clear`) by the same modifier that drives normal invisibility, camera feeds render him invisible
for free.

## Known follow-ups

- Role icon (`Resources/RoleIcons/InvisibleBoy.png`) is placeholder art (generated, not final) — swap
  out when real art exists.
- `OneWayShadows` (used on Polus) reads as blocking sight in both directions in the current occlusion
  check — a true one-way shadow should only block from one side. Not yet specifically handled.
- The `Constants.ShadowMask` occlusion check and the 0.5s unseen grace period are first-guess tunables,
  not exhaustively tuned against real play — expect adjustment after live testing.
- Freeplay dummies count as watchers under the "living player" rule. This is useful for solo testing
  the passive without a second real player, and is considered correct rather than a bug.
