# RC-XD

Impostor Killing role inspired by the RC-XD killstreak from Call of Duty. Press Deploy to spawn an RC car
at your feet: you freeze in place, your movement controls drive the car instead (default 2× player speed,
configurable), and the camera follows the car. Press the button again (now labeled Detonate) to explode
the car, killing every player within the explosion radius — including other impostors when "RC-XD Can Kill
Impostors" is on (default On), and even the deployer if they're in the blast. If not detonated within the
Drive Time (default 8 s), the car despawns harmlessly and control snaps back. A meeting or the RC-XD's
death also despawns the car harmlessly. The RC-XD keeps the normal kill button. Everyone can see the car.

Files: `Roles/Impostor/RcXdRole.cs`, `Buttons/Impostor/RcXdDeployButton.cs`, `Modules/RcXdCar.cs`,
`Modules/RcXdCarBehaviour.cs`, `Options/Roles/Impostor/RcXdOptions.cs`.

## How it works

**Two-phase button via cancellable effect.** Deploy and Detonate are the same button with a sprite/name
swap. `IsEffectCancellable()` is true so that `CanClick()` passes during the effect, but the second press
is handled in our own `ClickHandler` override — NOT MiraAPI's cancellable-effect branch, because TOU's
`TownOfUsButton.ClickHandler` fully replaces MiraAPI's and drops that branch (it would go straight to
`OnClick()`, redeploying the car — see the gotchas doc). Our override replicates TOU's hacked/disabled
gating, sets the `detonateRequested` flag, and calls `ResetCooldownAndOrEffect()`, which starts the
deploy cooldown and fires `OnEffectEnd()`. The flag distinguishes "player pressed detonate" from "timer
expired naturally" — both paths funnel through `OnEffectEnd()` but take different actions.

**CanUse must stay true while the effect is active.** MiraAPI's `CanClick()` is
`(EffectActive ? IsEffectCancellable() : Timer <= 0) && CanUse()`
(`reference/MiraAPI/MiraAPI/Hud/CustomActionButton.cs`), so gating `CanUse()` on `!EffectActive` (as the
Sniper does) would make the Detonate press dead. Worse, the TOU base `TownOfUsButton.CanUse()` fails its
`PlayerControl.LocalPlayer.CanMove` check while the driver is frozen (`moveable = false` makes `CanMove`
false) — so while `EffectActive`, our `CanUse()` bypasses `base.CanUse()` entirely and replicates only the
guards that still apply (alive, not rewinding, not ability-disabled). Hacked/disabled are re-checked by
TOU's own `ClickHandler` on top.

**Car is a plain GameObject, not an InnerNetObject.** Position sync uses throttled Reactor
[MethodRpc] broadcasts (~10/s) following the NinjaTraces pattern (see `Modules/NinjaTraces.cs`), not a
custom NetTransform. Only the deployer's client has a Rigidbody2D (collides with the ship's wall
layer via Physics2D.IgnoreCollision against all player colliders). Remote clients interpolate toward
the latest RPC'd position every frame via `MoveTowards`.

**Freeze, camera, and light reparenting.** Copied verbatim from TOU-Mira's MedSpiritObject (lines
118–131, 165–178): `moveable = false`, `NetTransform.SetPaused(true)` + `ClearPositionQueues()`,
`SetKinematic(true)` to freeze; `HudManager.Instance.PlayerCam.SetTarget(car.GetComponent<RcXdCarBehaviour>())`
(MonoBehaviour, not Transform — verified API quirk) to follow the car; `lightSource.transform.parent`
re-pointed at the car so vision follows. The ShadowQuad is **not** touched — no wall vision while driving
the car (design decision).

**Detonation kills computed owner-side only.** When `RpcDetonateCar` fires, only the deployer's client
computes targets and calls `RpcSpecialMultiMurder` once. Targets come from `Helpers.GetClosestPlayers(carPos,
ExplosionRadius × ShipStatus.Instance.MaxLightRadius)`, filtered to skip dead/disconnected/in-vent
players, `FirstDeadShield` holders, and `DisabledModifier`s with `!CanBeInteractedWith` (same filter as
`SniperShots.FindHits`).
Additionally, `IsImpostorAligned()` players are filtered out when `CanKillImpostors` is false. The deployer
stays in the target list — self-kill by design. `RpcSpecialMultiMurder` is an RPC itself, so it fans out to
all clients; verified to work with the source as a target.

**Cancel paths mirror Sniper — and always restore before sending RPCs.** The button's FixedUpdate
checks `playerControl.HasDied() || MeetingHud.Instance` while the effect is active and fizzles directly —
sets `EffectActive = false`, `SetTimer(Cooldown)`, clears `detonateRequested`, calls the idempotent
`EndDrive()`, and only then sends `RpcDespawnCar` inside a try/catch (same shape as SniperSnipeButton's
cancel; `OnEffectEnd()` is NOT invoked on this path). `OnEffectEnd()` uses the same restore-first order.
FixedUpdate also self-heals two stale states every tick: "effect active but no car" cancels as a fizzle,
and "drive lock set but no active effect" runs `EndDrive()` — both guard against exceptions or cross-game
leftovers (button singletons persist for the whole process, see docs/il2cpp-gotchas.md). It also
re-asserts `moveable = false` every tick — vanilla animations flip it back on ("self-heal doctrine", see
Sniper notes). The car behaviour additionally self-destroys locally when `MeetingHud.Instance` appears —
a safety net for remote clients in case the deployer's fizzle RPC races the meeting — and
`RcXdCar.DestroyLocally` rescues the driver's lightSource and camera off the car before destroying it, on
every destroy path.

## Design decisions

- **RPC position sync chosen over InnerNetObject.** The car only exists for ~8 s and doesn't need
  persistent state or complex ownership logic. An InnerNetObject would require an asset-bundle prefab +
  ~300 lines of custom NetTransform boilerplate (reference: TOU-Mira's MedSpirit). Reactor's throttled
  RPC pattern is simpler and sufficient. Upgrade path exists if future roles need persistent network
  entities.
- **Deployer can die to own car.** User decision, CoD-faithful. No self-immunity check.
- **Car collides with walls.** Player collision is disabled (ignored), but Rigidbody2D on the driver's
  client respects the ship's wall layer, so the car can't clip through obstacles.
- **No wall vision while driving.** Unlike the Sniper's aim mode (which disables ShadowQuad), the RC-XD
  driver sees the game normally — the car is on the ground, immersion-breaking to suddenly see through
  walls while "inside" it.
- **Everyone sees the car.** No stealth option; the car is a visual presence for all players.

## Known follow-ups

- **Real car sprite.** In-world car is currently the Rewind button placeholder (looks like a clock).
  Deploy and Detonate buttons also use the same placeholder.
- **Explosion VFX.** Currently a short-lived ignite-material sphere flash (Sentinel `Explode` pattern)
  plus the Arsonist ignite sound — both placeholders.
- **Observer smoothing quality.** Remote clients interpolate toward each RPC'd position via `MoveTowards`
  every frame. Playtest will reveal if the ~10/s broadcast frequency feels jittery. If so, increase
  broadcast rate or tune `MoveTowards` speed.
- **Verify RpcSpecialMultiMurder self-kill in practice.** Documentation claims it doesn't exclude the
  source, but edge-case testing under different network conditions is needed. Fallback: separate
  `RpcCustomMurder` on self if the documented behavior doesn't hold.

## Playtest history

- **2026-07-17 first playtest (freeplay): "everything goes black after the RC-XD fails and the camera
  goes back" + "after that, giving myself RC-XD in freeplay greys out all buttons except sabotage,
  even after leaving and re-entering — until restart".** Most consistent root-cause chain (the
  original code ordered the fizzle as despawn-RPC-first, restore-second): the despawn destroyed the
  car GameObject the player's `lightSource` was parented to → light destroyed → permanent black
  screen; and something in the RPC path threw, skipping `EndDrive()` entirely → `driveLockActive`
  stuck true on the process-lifetime button singleton → every later game with the RC-XD role
  re-froze the player each tick (`moveable = false` → `CanMove` false → all TOU/vanilla buttons
  greyed; sabotage panel opens but nothing works). Fixes: restore-first ordering on every exit path
  with the RPC send in try/catch (which now logs the actual exception to `BepInEx/LogOutput.log` if
  it recurs — grab any `RC-XD:` lines on a re-test); FixedUpdate stale-state self-heals;
  light/camera rescue in `RcXdCar.DestroyLocally`; explicit `localPosition = zero` after light
  reparenting (reparenting keeps world position). See the "Button singletons persist" entry in
  docs/il2cpp-gotchas.md. Needs a re-test.
- **2026-07-17 second playtest: "pressing Detonate teleports the car back to me and restarts the
  countdown instead of exploding".** Root cause: the design assumed MiraAPI's `ClickHandler`
  cancellable-effect branch would catch the second press, but `TownOfUsButton.ClickHandler` fully
  replaces MiraAPI's handler and has no such branch — with the effect active it went straight to
  `OnClick()`, which redeployed the car at the player's feet and re-armed the effect timer. Fix: the
  effect-active press is now handled entirely inside `RcXdDeployButton.ClickHandler` (TOU
  hacked/disabled gating replicated, then `detonateRequested = true` +
  `ResetCooldownAndOrEffect()`); TOU's base handler only runs for the Deploy press. See the extended
  "Overriding `TownOfUsButton.ClickHandler`" entry in docs/il2cpp-gotchas.md. Needs a re-test.

## Playtest checklist

- **Deploy freezes player and follows with camera.** Press Deploy—player immobilizes, camera snaps to
  car. Walk/strafe animation still plays (animation self-heal — this is acceptable per Sniper notes,
  but watch for it looking broken vs. intentional).
- **Car drives at 2× player speed and stops at walls.** Movement controls move the car; it collides with
  obstacles and doesn't clip through, and doesn't shove players around.
- **Detonate kills in radius.** Press Deploy, drive close to another player, press again
  (button is now labeled Detonate) — target dies with cause "Exploded".
- **Can Kill Impostors toggle works.** Enable RC-XD in setup, toggle off "RC-XD Can Kill Impostors" in
  options, deploy and detonate near an impostor — they survive.
- **Deployer dies to own blast.** Drive car to a corner, detonate while inside — deployer dies.
- **8 s expiry despawns harmlessly.** Deploy, do not press again, wait 8 s — car vanishes, control
  restored, button returns to Deploy with cooldown.
- **Meeting despawns car harmlessly.** Deploy, call meeting before expiry — car vanishes, control
  restored, button reverts to Deploy.
- **Deployer death despawns car.** Deploy, get killed by another impostor — car vanishes for everyone.
- **Observer sync.** On a second client: see the car spawn, move smoothly-ish across the map, and
  vanish on detonate/expiry/meeting/death.
- **Confirm no stuck camera/freeze on any cancel path.** All three exits (detonate, expiry, cancel)
  should restore movement and camera to the player.
