# RC-XD

Impostor Killing role inspired by the RC-XD killstreak from Call of Duty. Press Deploy to spawn an RC
car at your feet: you freeze in place, your movement controls drive the car instead (default 2× player
speed, configurable), and the camera follows the car. Press the button again (now labeled Detonate) to
explode the car, killing every player within the explosion radius — including other impostors when
"RC-XD Can Kill Impostors" is on (default On), and even the deployer if they're in the blast. The
camera lingers on the blast site for ~1.5s before returning to the player. If not detonated within the
Drive Time (default 8s), the car despawns harmlessly. A meeting or the RC-XD's death also despawns the
car harmlessly. The RC-XD keeps the normal kill button. Everyone can see the car.

Files: `Roles/Impostor/RcXdRole.cs`, `Buttons/Impostor/RcXdDeployButton.cs`, `Modules/RcXdCar.cs`,
`Modules/RcXdCarBehaviour.cs`, `Options/Roles/Impostor/RcXdOptions.cs`, `Events/RcXdEvents.cs`.

## How it works

**Two-phase button via a manual `ClickHandler` override.** Deploy and Detonate are the same button
with a sprite/name swap. `TownOfUsButton.ClickHandler` fully replaces MiraAPI's and has no
cancellable-effect branch, so the Detonate press is handled entirely in our override rather than
relying on `IsEffectCancellable()` + `base.ClickHandler()` (see docs/il2cpp-gotchas.md). `CanUse()`
also bypasses `base.CanUse()` while the effect is active, since the TOU base's `CanMove` check would
otherwise fail while the driver is deliberately frozen.

**Car is a plain GameObject, not an InnerNetObject.** Position sync is throttled Reactor `[MethodRpc]`
broadcasts (~10/s), following the `NinjaTraces` pattern rather than a custom NetTransform. Only the
deployer's client has a `Rigidbody2D` (collides with the ship's walls via layer match, ignores
collision with all player colliders). Remote clients interpolate toward the latest RPC'd position.

**Freeze, camera, and light reparenting.** Adapted from TOU-Mira's `MedSpiritObject`: `moveable =
false` + `NetTransform.SetPaused(true)` + `SetKinematic(true)` to freeze; `PlayerCam.SetTarget(car)`
to follow (`FollowerCamera.SetTarget` takes a `MonoBehaviour`, not a `Transform`); `lightSource`
reparented to the car so vision follows. `ShadowQuad` is untouched — no wall vision while driving
(design decision).

**Post-detonation camera linger.** On detonate, the restore is deferred: `BeginCameraLinger` parks the
camera and light on an inert anchor GameObject (an uninitialized `RcXdCarBehaviour`, since `SetTarget`
needs a MonoBehaviour) at the blast position before the detonate RPC destroys the car, so the driver
watches the explosion for `CameraLingerDuration` (1.5s). Ticked down in `FixedUpdate`, not a coroutine,
so the restore can't be stranded by an exception; ends early on meeting start or the driver's own death.
**The linger is skipped entirely when the deployer is inside their own blast**: the kill runs
synchronously inside the detonate RPC (see the "murders are synchronous" note in
docs/il2cpp-gotchas.md), so the drive state is fully restored while still alive and the death then
proceeds on vanilla state — the explosion is on top of the player anyway, so nothing is missed.

**Detonation kills are computed owner-side only.** `RpcDetonateCar` runs on every client for the visual
(explosion sphere + sound), but only the deployer (`owner.AmOwner`) computes targets via
`Helpers.GetClosestPlayers` — filtered like `SniperShots.FindHits` (alive, not vented, not shielded,
not `DisabledModifier`-protected) plus `IsImpostorAligned()` when `CanKillImpostors` is off. Other
victims go through `RpcSpecialMultiMurder`; the deployer's own death (self-kill by design) goes
through a separate `RpcCustomMurder(owner, owner)` — the Sheriff-misfire pattern — because the
multi-murder pipeline hard-crashes on source == target (docs/il2cpp-gotchas.md).

**Cancel paths always restore before sending RPCs, and stale state self-heals.** Death or a meeting
mid-drive fizzles the effect: restore first (`EndDrive()`), then send the despawn RPC in a try/catch
that falls back to `RcXdCar.EnsureDestroyedLocally()`. `FixedUpdate` also self-heals two stale states
every tick ("effect active but no car" and "drive lock set but no active effect") to survive exceptions
or cross-game leftovers, since button singletons persist for the whole process (docs/il2cpp-gotchas.md).
`RcXdCar.DestroyLocally` rescues the driver's light/camera off the car before destroying it, on every
destroy path.

## Design decisions

- **RPC position sync over InnerNetObject.** The car only exists for ~8s; an InnerNetObject would need
  an asset-bundle prefab and ~300 lines of custom NetTransform boilerplate. Upgrade path exists if a
  future role needs a persistent network entity.
- **Deployer can die to their own car.** User decision, CoD-faithful. No self-immunity check.
- **No wall vision while driving.** Unlike the Sniper's aim mode, the driver sees the game normally.
- **Everyone sees the car.** No stealth option.

## Known follow-ups

- **Deploy/Detonate button art + explosion VFX.** The two HUD buttons and the explosion flash are
  still placeholders (Rewind sprite, Sentinel `Explode`/Arsonist ignite sound patterns). The in-world
  car itself has real art (`Resources/ImpButtons/RcXdCar.png`, `SuperSquadImpAssets.RcXdCarSprite`,
  450 pixels/unit) — tune that pixels-per-unit value if the car reads too big/small in a live playtest.
- **Observer smoothing quality.** Remote clients `MoveTowards` toward each RPC'd position; playtest
  will show if ~10/s feels jittery.

Lessons from two fixed bugs, worth keeping in mind for similar roles:

- A disconnect mid-drive has no "keep ticking" fix the way death does (see the `Enabled()` trick in
  docs/il2cpp-gotchas.md) — a disconnected player has no client left running their button's
  `FixedUpdate` at all. Needed a `PlayerLeaveEvent` hook instead (`Events/RcXdEvents.cs`, mirroring
  `DumperEvents`/`PelicanEvents`/`DaddyHagridEvents`) that despawns the car directly on disconnect
  rather than waiting for the meeting-time safety net in `RcXdCarBehaviour`.
- `RpcSpecialMultiMurder`'s `List<PlayerControl>` overload silently defaults to `MeetingCheck.Ignore`
  (no compiler warning) unless `MeetingCheck.OutsideMeeting` is passed explicitly — TOU-Mira's own
  Bomber does this via `Bomb.cs`. Other call sites in this codebase (`SuperSquadDetonator`,
  `SentinelExplodeButton`, `SniperSnipeButton`) use the same implicit-`Ignore` overload and may want
  the same audit.

## Playtest history

Full play-by-play (including a few wrong-turn diagnoses along the way) is in git history; this is
the lessons that stuck.

- **2026-07-17: post-fizzle black screen, then every button greyed in later games.** Restore must
  happen before any RPC/destroy call that can throw, and `FixedUpdate` must self-heal stale state —
  see the "button singletons persist" entry in docs/il2cpp-gotchas.md. Fixed via restore-first
  ordering, `FixedUpdate` self-heals, and the light/camera rescue in `RcXdCar.DestroyLocally`.
- **2026-07-17: Detonate press redeployed the car instead of exploding it.** `TownOfUsButton
  .ClickHandler` has no cancellable-effect branch, so the second press fell through to `OnClick()`.
  Fixed by handling the effect-active press entirely inside the `ClickHandler` override.
- **2026-07-17: self-kill left the camera stuck at the blast site.** Dying swaps `Data.Role` to a
  ghost role, so MiraAPI stops driving the button's `FixedUpdate` (see "buttons stop ticking on
  death" in docs/il2cpp-gotchas.md) before the linger/restore logic in it could run. Fixed by
  overriding `Enabled` to stay true while any drive/linger state is pending.
- **2026-07-17 to present: self-kill repeatedly hard-crashes the game — NOT fixable from this
  addon.** Several addon-side theories (vanilla `KillOverlay.ShowKillAnimation` throwing for
  killer == victim; MiraAPI's version-gated `DeepDestroy`/asset-unload GC pass) were tried and each
  falsified by re-testing - same crash signature persisted regardless. Actual cause, confirmed by a
  TOU-Mira maintainer: known, currently-unfixed memory corruption in TOU-Mira 1.6.3-beta2 (the
  latest available release) tied to AU 2026.6.5 itself - "unavoidable stability issues... we will
  likely have to wait until the next update" ([AU-Avengers/TOU-Mira#197](https://github.com/AU-Avengers/TOU-Mira/issues/197)).
  This explains why it's role-agnostic and reproduces on a plain TOU Sheriff misfire with zero
  addon code involved; this addon only raises the odds of hitting it by adding more live
  objects/types. See "Local-death native crash" in docs/il2cpp-gotchas.md for the full trail and
  what to check when a TOU-Mira update lands. `Patches/SelfKillOverlayPatch.cs` was kept since it
  fixes a real, separate, confirmed-broken vanilla method, but it does not fix this crash. One real
  RC-XD bug found along the way (unrelated to the crash): the self-kill call had relied on MiraAPI's
  `teleportMurderer` default (`true`, which yields mid-kill for a blur animation) instead of the
  explicit `false` this role's restore-before-detonate design requires.
- **2026-07-19: deployer's cause of death didn't read "Exploded".** MiraAPI's `RpcCustomMurder` has
  no `causeOfDeath` parameter and skips TOU's death-handler bookkeeping entirely. Fixed by calling
  `DeathHandlerModifier.RpcUpdateLocalDeathHandler` with the `DiedToSuperSquadRcXd` locale key
  before the self-kill.
- **2026-07-19: one F press deployed, detonated, AND opened the ghost Haunt menu — two separate
  bugs.** (1) The same press double-dispatched to this button's own `ClickHandler` (same-frame
  double dispatch or key autorepeat under Proton). Fixed with a 0.3s `DetonateArmDelay` between
  Deploy and the first accepted Detonate press. (2) Deploy/Detonate share the vanilla Ability
  keybind with the ghost's Haunt menu (`Keybinds.SecondaryAction` *is* `AbilityButton`); that
  button polls the key independently each frame, so once the self-kill flips the player to a ghost
  role, the same still-down key opens Haunt later the same frame. Fixed with
  `Patches/SuppressHauntAfterSelfDetonatePatch.cs`, which skips that click for the exact frame
  `RcXdCar.SelfDetonationFrame` marks. Needs a re-test.

## Playtest checklist

- Deploy freezes the player and the camera follows the car; car drives at 2× and stops at walls.
- Detonate kills everyone in radius with cause "Exploded"; toggling off "Can Kill Impostors" spares
  impostors; the deployer dies if caught in their own blast.
- A single F press deploys OR detonates, never both, and a self-kill via hotkey does not open the
  ghost Haunt menu.
- Camera lingers on the blast for ~1.5s (frozen the whole time) before snapping back; a meeting cuts
  it short cleanly.
- Self-kill skips the linger: detonating inside the blast radius restores control instantly, then
  the normal death plays out at your position, camera follows the ghost, ghost can fly. A crash here
  is a known open upstream TOU-Mira issue (see Playtest history), not something to keep re-diagnosing
  as an RC-XD bug.
- 8s expiry despawns harmlessly and restores control/camera/light; same for a meeting or the
  deployer's death mid-drive.
- Second client sees the car spawn, move smoothly, and vanish on every exit path.
