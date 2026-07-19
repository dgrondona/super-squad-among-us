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
`Modules/RcXdCarBehaviour.cs`, `Options/Roles/Impostor/RcXdOptions.cs`.

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
- **Deployer's cause of death may not read "Exploded".** The self-kill goes through
  `RpcCustomMurder`, which (in pinned MiraAPI 0.3.5) has no `causeOfDeath` parameter, so the
  deployer's death cause falls back to TOU's generic handling. Cosmetic only.

## Playtest history

- **2026-07-17, freeplay: black screen after a fizzle, then all buttons greyed in every later game.**
  Root cause: despawn ran before restore (destroyed the car's light) and an exception skipped
  `EndDrive()`, sticking `driveLockActive` true on the process-lifetime button singleton. Fixed via
  restore-first ordering, FixedUpdate self-heals, and light/camera rescue in `RcXdCar.DestroyLocally`.
  Needs a re-test.
- **2026-07-17: pressing Detonate redeployed the car instead of exploding it.** Root cause:
  `TownOfUsButton.ClickHandler` has no cancellable-effect branch, so the second press fell through to
  `OnClick()`. Fixed by handling the effect-active press entirely inside the `ClickHandler` override.
  Needs a re-test.
- **2026-07-17: self-kill from the blast left the camera stuck at the explosion site as a ghost.**
  First fix attempt (reordering `EndDrive()` to restore camera/light before movement) didn't help,
  because `EndDrive` was never reached at all: dying swaps `Data.Role` to a ghost role, MiraAPI stops
  driving the button's `FixedUpdate` once `Enabled(role)` is false, and the linger countdown /
  death-cancel logic lived in that `FixedUpdate`. Real fix: override `Enabled` to stay true while
  `EffectActive`/`driveLockActive`/`cameraLingerActive` is pending (see the "buttons stop ticking on
  death" entry in docs/il2cpp-gotchas.md). The reorder was kept as defense-in-depth, and `EndDrive`
  always undoes the `SetKinematic`/`SetPaused` state even for a ghost (a kinematic body can't fly).
- **2026-07-17/18: with that fix, self-kill hard-crashed the game (native crash, BepInEx log tail
  lost; Player-prev.log truncates during the multi-murder death processing).** The kills are fully
  synchronous inside `RpcDetonateCar` (no yields with `teleportMurderer: false`), so the deployer is
  already a ghost mid-death-teardown when the linger's ghost-tick restore ran — restoring drive
  state at that point is what crashed. Fix: pre-check whether the deployer is inside the blast
  (same `Helpers.GetClosestPlayers` query the kill uses, gated on `CanKillImpostors`) and if so run
  the normal alive-path `EndDrive()` BEFORE sending the detonate RPC, skipping the linger entirely.
  If a mid-drive or mid-linger death by an external killer still crashes on re-test, the culprit is
  specifically `EndDrive`-on-a-ghost and needs the same restore-before-death treatment.
- **2026-07-18: still crashed with the restore-first fix — and this time the BepInEx log caught it:
  `RC-XD car detonated: killed 1 players` → TOU's `UpdateDeathHandlerImmediate` error → hard crash
  with all RC-XD state already restored and cleared.** Suspected `RpcSpecialMultiMurder` crashing
  when the source is among its own targets; split the kill (other victims via multi-murder, the
  deployer via `RpcCustomMurder(owner, owner)`, the Sheriff-misfire call). Still crashed after this
  fix — see the next entry, this diagnosis was wrong.
- **2026-07-18: root cause found — the addon was compiled against stale package versions.** A
  control test (plain TOU Sheriff misfire self-kill, zero RC-XD code involved) crashed identically,
  and stopped crashing with the addon DLL removed entirely — proving the bug was version skew, not
  RC-XD logic. `AmongUs.props` was pinned to TownOfUsMira 1.5.0-beta.1 / MiraAPI 0.3.5 / Reactor
  2.5.0-ci.371 while the installed runtime was 1.6.3-beta2 / 0.4.1 / 2.5.1. Fixed by bumping the
  pinned versions to match and fixing the resulting compile breaks (`ModifierFaction` moved
  namespace; `VanillaTweakOptions.PetVisibilityUponDeath` finally landed for real, replacing
  `SuperSquadBodies`' manual reimplementation — see docs/il2cpp-gotchas.md). The
  `RpcCustomMurder(owner, owner)` self-kill split from the previous entry was kept (harmless,
  possibly still marginally safer) but is no longer believed necessary on its own. Needs a re-test
  on the version-matched build before any further RC-XD-specific investigation.

## Playtest checklist

- Deploy freezes the player and the camera follows the car; car drives at 2× and stops at walls.
- Detonate kills everyone in radius with cause "Exploded"; toggling off "Can Kill Impostors" spares
  impostors; the deployer dies if caught in their own blast.
- Camera lingers on the blast for ~1.5s (frozen the whole time) before snapping back; a meeting cuts
  it short cleanly.
- Self-kill skips the linger: detonating inside the blast radius restores control instantly, then
  the normal death plays out at your position — no crash, camera follows the ghost, ghost can fly.
- 8s expiry despawns harmlessly and restores control/camera/light; same for a meeting or the
  deployer's death mid-drive.
- Second client sees the car spawn, move smoothly, and vanish on every exit path.
