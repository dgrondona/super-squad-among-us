# Invisible Boy

Crewmate Support role. Passive: automatically invisible whenever no living player has line of sight to
him; visible the instant anyone can see even a sliver of him.

Files: `Roles/Crewmate/InvisibleBoyRole.cs`, `Modifiers/InvisibleBoyModifier.cs`,
`Modules/SightChecker.cs`, `Patches/InvisibleBoyVisibilityPatch.cs`, `Patches/InvisibleBoyAdminPatch.cs`.

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

### Per-client visibility computation

Visibility is computed **locally on every client**, not just the Invisible Boy's own, and applied
locally with **no RPC**: `InvisibleBoyVisibilityPatch` (a `PlayerControl.FixedUpdate` postfix that runs
for every player on every client, unlike the role's own tick and MiraAPI's own-owner-only button tick)
calls `InvisibleBoyRole.UpdateVisibilityState`, which evaluates `SightChecker.CanAnyoneSee` and locally
adds/removes the `InvisibleBoyModifier` (`AddModifier`/`RemoveModifier`, not the `Rpc*` variants). Every
observer reaches the same conclusion from the shared, networked player positions, so the state stays
consistent without a networked authority.

This is deliberately not the more obvious "the owner computes it and RPCs the result" design: that
version can't drive a player this client doesn't own — most importantly a **practice-mode dummy**, which
has no owning client running role logic — so an invisible dummy would never actually turn invisible.
Computing per-observer makes the passive work against dummies (so it's solo-testable) and removes the
owner as a single point of failure.

Rendering reuses `TownOfUsAppearances.Swooper`-style handling: fully `Color.clear` to other players, and
a ghostly ~0.1-alpha self-view so the Invisible Boy can still see their own outline. Reusing the Swooper
appearance path means this doesn't fight comms-camouflage sabotages that hook the same path.

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

The admin table (and the Spy's map, same overlay) is hidden via a Harmony prefix/postfix pair on
`MapCountOverlay.Update`. The count overlaps colliders against each room and resolves every hit back to
its owner with `collider.GetComponent<PlayerControl>()`, so the prefix disables the invisible player's
colliders for the duration of that count and the postfix restores exactly what it disabled (not a blanket
re-enable, in case something else legitimately had them off).

**Disable *all* colliders on his object, not just `PlayerControl.Collider`.** A player carries more than
one `Collider2D` on their own GameObject (the movement body *and* the click-to-kill collider); the overlap
resolves either one to the same `PlayerControl`, so disabling only `.Collider` left the other to be
counted and he still showed on admin. The prefix iterates `player.GetComponents<Collider2D>()` and
disables each (child colliders can't be resolved by `GetComponent`, so they never count). Patch priority
is `Priority.High` so this runs *before* TOU-Mira's own Spy-role prefix on the same method — ordering
matters here since Spy's logic also inspects collider state during the count.

### Cameras

Security cameras (and TOU-Mira's `IsVisibleToOthers`) honor the vanilla `PlayerControl.Visible` flag,
**not** the sprite alpha the appearance system sets — so the transparent-appearance trick that hides the
main view does *not* hide camera feeds on its own (the same gap Swooper has). The modifier therefore
also drives `Player.Visible` in `ApplyLocalVisibility()`, called from `OnActivate`/`FixedUpdate`: on any
client whose local viewer should see nothing (a living non-owner), the invisible player is set
`Visible = false`, fully removing him from that client's main view and cameras alike. It's re-asserted
every tick because vanilla flips `Visible` back on across vent/ladder animations.

`Visible` is a **local, per-client** rendering flag, so this is decided independently on each client: the
owner's own `Visible` is never touched (he keeps his ghost outline, and his vent/ladder visibility stays
vanilla-managed), and dead-who-know viewers are likewise left visible so they keep seeing the outline.

`RendererColor` (the faint outline alpha for dead-who-know viewers, set in `GetVisualAppearance()`) needs
the same per-tick treatment: it's only actually written to the sprite when `RawSetAppearance` runs, so
`ApplyLocalVisibility()` calls it every tick too, not just once from `OnActivate` — otherwise a viewer
whose dead-know status changes after activation stays visible-but-transparent until deactivation.

> Testing note: because the passive is computed per-observer (see above), a practice-mode dummy set to
> Invisible Boy **is** solo-testable. A dummy isn't owned by your client, so you are a third-party
> observer to it and see full invisibility (not the self-outline). You are also a watcher, though, so
> break line of sight for it to vanish: stand behind a wall or beyond your vision range from the dummy
> and it disappears from your view, the cameras, and the admin table; step back into sight and it
> reappears instantly.

## Known follow-ups

- Role icon (`Resources/RoleIcons/InvisibleBoy.png`) is placeholder art (generated, not final) — swap
  out when real art exists.
- `OneWayShadows` (used on Polus) reads as blocking sight in both directions in the current occlusion
  check — a true one-way shadow should only block from one side. Not yet specifically handled.
- The `Constants.ShadowMask` occlusion check and the 0.5s unseen grace period are first-guess tunables,
  not exhaustively tuned against real play — expect adjustment after live testing.
- Freeplay dummies count as watchers under the "living player" rule. This is useful for solo testing
  the passive without a second real player, and is considered correct rather than a bug.
