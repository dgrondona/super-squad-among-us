# IL2CPP / namespace gotchas

Things that look like a normal C# base-game mod but aren't, learned the hard way while building roles
in this repo.

## IL2CPP-injected classes

- Role classes require an `(IntPtr cppPtr)` primary constructor forwarding to the base class (see any
  existing `RoleName(IntPtr cppPtr) : BaseRole(cppPtr)` declaration).
- They cannot call `base.Method()` directly for overridden vanilla `RoleBehaviour` methods (e.g.
  `Initialize`, `Deinitialize`). When you need to run the original vanilla behavior alongside custom
  logic, call the matching reverse-patch trampoline in `MiraAPI.Patches.Stubs.RoleBehaviourStubs`
  instead of `base.X()`. If you don't need any custom init/deinit logic, simply don't override the
  method at all — the simplest existing roles (e.g. Apparater) do this.

## Global usings don't cross the assembly boundary

TOU-Mira's `global using`s (declared in its own `GlobalUsings.cs`) are scoped to the TownOfUsMira
compilation and are **not** inherited by this project just because it references the compiled DLL.
This project's own `GlobalUsings.cs` only aliases the Reactor logger, so every file must explicitly
`using` whatever TOU-Mira/MiraAPI namespace it needs. Namespaces that are easy to get wrong because
they don't match their folder name in the TOU-Mira source tree:

| Type | Namespace | Folder it actually lives in |
|---|---|---|
| `IDoomable`, `DoomableType` | `TownOfUs.Extensions` | `Interfaces/` |
| `TouLocale` | `TownOfUs.Modules.Localization` | — |
| `MiscUtils` | `TownOfUs.Utilities` | — |
| `BaseKeybind` | `MiraAPI.Keybinds` | — |
| `SetOutline(Renderer, Color?)` | `Reactor.Utilities.Extensions` | Reactor, not TOU/Mira |

The `SetOutline` row is an extension-resolution trap, not just a missing type: without the Reactor
using, `x.SetOutline(colorOrNull)` resolves to TOU-Mira's `SetOutline(this Vent, bool, bool, Color)`
and fails with a confusing CS7036 about a missing `mainTarget` argument.

When in doubt, `grep` the namespace declaration directly in `reference/TOU-Mira` or `reference/MiraAPI`
rather than guessing from the folder path — the build (`dotnet build SuperSquadAmongUs.sln`) will
immediately flag `CS0246` for anything missing, which is the fastest way to catch these.

## Locale merge ordering

`Modules/SuperSquadLocale.cs` hooks `IL2CPPChainloader.Instance.Finished` (wired up in
`SuperSquadAmongUsPlugin.Load()`) so this mod's locale XML is merged into TOU-Mira's `TouLocale`
registry *after* TOU-Mira has already loaded its own — hooking earlier would get overwritten or fail
to find TOU-Mira's registry yet.

## Auto-registration means typos fail silently, not loudly

Since roles/buttons/options are picked up by reflection (see [architecture.md](architecture.md)),
there's no compiler error if e.g. a role class doesn't actually implement `ICustomRole` correctly, or
an option group isn't a subclass of `AbstractOptionGroup<T>` — it just won't show up in-game. If a new
role/button/option "does nothing" after a clean build, check the interface/base-class list first
before assuming a networking or logic bug.

## Mouse clicks must be polled per rendered frame, never in button FixedUpdate

MiraAPI button `FixedUpdate(PlayerControl)` runs on `PlayerControl.FixedUpdate`'s fixed tick, and
`Input.GetMouseButtonDown` is only true during the single rendered frame of the press — polling it
from the fixed tick silently drops most clicks whenever FPS exceeds the tick rate. This has now
bitten twice (Apparater map click, Sniper aim click). The fix both times: a `HudManager.Update`
postfix (`Patches/ApparaterMapClickPatch.cs`, `Patches/SniperAimPatch.cs`) that calls a self-guarding
handler on the button. Also record `Time.frameCount` when the ability is armed so the arming click
can't double as the action click in the same frame. The keybind variant of the same bug: a single
physical keypress can dispatch to a button's ClickHandler more than once (same-frame double
dispatch, or key autorepeat - observed under Proton), so a two-phase button (Deploy/Detonate on one
key) needs a short time-based arming delay, not just a frame check - see
`RcXdDeployButton.DetonateArmDelay`.

## `EventSystem.IsPointerOverGameObject()` does not see Among Us HUD buttons

The vanilla HUD (kill/use/report/sabotage/custom buttons) is collider-based `PassiveButton`s, not
uGUI, so the EventSystem check misses them entirely. To test "did this click land on HUD?", also
probe the UI layer under the cursor: `Physics2D.OverlapPoint(HudManager.Instance.UICamera
.ScreenToWorldPoint(Input.mousePosition), LayerMask.GetMask("UI"))`. See
`SniperSnipeButton.IsClickOnHud()`.

## Overriding `TownOfUsButton.ClickHandler` drops the hacked/disabled gating

TOU-Mira's base `ClickHandler` checks `GlitchHackedModifier` and `DisabledModifier` before running
the click. Any override must re-add both checks (see `NinjaMarkButton.ClickHandler`), and any custom
non-button trigger path (like the Sniper's world click) needs the same gating manually.

The reverse also bites: `TownOfUsButton.ClickHandler` fully REPLACES MiraAPI's `ClickHandler` and
does not replicate its cancellable-effect branch (`if (EffectActive && IsEffectCancellable()) {
ResetCooldownAndOrEffect(); return; }`). A TOU-based button that wants "press again during the
effect" behavior cannot rely on `IsEffectCancellable()` alone — with the effect active, TOU's handler
goes straight to `OnClick()` and re-arms the effect instead. Handle the effect-active press in your
own `ClickHandler` override (gating included) and call `ResetCooldownAndOrEffect()` yourself — see
`RcXdDeployButton.ClickHandler`.

## MiraAPI vanilla events fire on every client — sync via local state changes, not host gating

`StartMeetingEvent`, `EjectionEvent`, and `PlayerDeathEvent` are invoked from postfixes on
`MeetingHud`, `ExileController.Begin`, and `PlayerControl.Die`, all of which run on all clients.
TOU-Mira's own handlers (e.g. `LoverEvents`) therefore apply kills with *local* calls
(`DeathHandlerModifier.UpdateDeathHandlerImmediate` + vanilla `player.Exiled()`) ungated — every
client performs the same deterministic change on its own copy. Do not add `AmHost` gates or RPCs in
these handlers; that's TOR's model, not TOU-Mira's. Deciding *which* client acts is only needed for
client-authoritative things like movement (`RpcSnapTo` from the owner).

## Vanilla kill/vent/ladder animations silently reset player state you disabled manually

Anything toggled once in a modifier's `OnActivate` (collider, `Player.Visible`, appearance) can get
flipped back by vanilla code mid-animation — `CustomMurder`/`CoPerformCustomKill`'s
`KillAnimation.SetMovement` re-enables the collider partway through a kill, and vent/ladder animations
similarly reset `Visible`/appearance (see `TimedInvisibilityModifier`). This bit the Astral: its
collider was disabled once on activation, so killing someone while phased silently re-enabled it and
ended wall-passing early even though the modifier and invisibility were still active. The fix is always
the same: don't just set the state once, self-heal it every `FixedUpdate` tick for as long as the
modifier is active (see `AstralFormModifier.FixedUpdate`, `TimedInvisibilityModifier.FixedUpdate`).

## When reference/ source doesn't cover it: decompile the real game assembly

`reference/TOU-Mira` and `reference/MiraAPI` are plain C# source, but the base game itself (roles,
`PlayerControl`, `LightSource`, cameras, etc.) is IL2CPP and not checked in anywhere in this repo. The
interop assembly BepInEx builds against is cached locally and is real, decompilable .NET metadata:

```
~/.cache/bepinex/game-libs/AmongUs.GameLibs.Steam/<version>/interop/<hash>/Assembly-CSharp.dll
```

(the exact `<version>` is whatever `AmongUs.props` pins). Decompile a type with `ilspycmd` (already
installed as a global dotnet tool):

```
ilspycmd -t <TypeName> <path-to-Assembly-CSharp.dll>
```

Method **bodies** are useless (IL2CPP native-call stubs, not real logic), but field/property/method
**signatures**, types, and inheritance are 100% accurate ground truth — this is the actual shipped
game, not a guess. Large types decompile slowly and verbosely; redirect to a file and `grep` for
declaration lines rather than reading the whole dump. This is how the ruled-out nametag-click theory (`PlayerControl.cosmetics.nameText` is a plain
`TMPro.TextMeshPro`, not a UI-raycastable `TextMeshProUGUI`) was confirmed. Note on the Sniper's wall
vision: decompiling found `ShipStatus.CalculateLightRadius`/`LightSource.viewDistance` as the light
radius mechanism, but the actual wall-occlusion overlay is `HudManager.ShadowQuad` (found later in
TOU-Mira source). This is a good lesson: decompiled metadata tells you what exists and its type, but
not which mechanism does what at runtime — cross-check against readable mod source when possible.

## `reference/TOU-Mira` can be ahead of the pinned `TownOfUsMira` package

The reference checkout is a live source tree; the package this addon actually compiles against is
whatever version is pinned in `AmongUs.props` (currently `1.5.0-beta.1`), which can lag behind it. A
member that exists in `reference/TOU-Mira` source may not exist yet in the compiled DLL, and reference
source can't stand in for that mismatch. Confirmed case: `VanillaTweakOptions.PetVisibilityUponDeath`
and the `PetHidden`-enum overload of `MiscUtils.RemovePet` exist in reference source but not in
1.5.0-beta.1 — the actual pinned API is a plain `HidePetsOnBodyRemove` bool option, checked alongside
`ShowPetsMode == PetVisiblity.AlwaysVisible`, and `RemovePet(PlayerControl)` takes no second argument
(see `SuperSquadBodies.DestroyBodies`). When a reference-source signature doesn't compile, decompile
the actual pinned DLL to check first (`~/.nuget/packages/townofusmira/<version>/lib/net6.0/TownOfUsMira.dll`,
same `ilspycmd -t <TypeName> <path>` recipe as above) before assuming the reference source is wrong.

## `Camera.main` can be transiently null — this codebase already defends against it, in several places

`Camera.main` does a tag-based scene lookup every call, not a cached reference, and TOU-Mira's own
source guards it defensively in multiple spots (e.g. `SentryCameraSurveillancePatch.cs` checks
`Camera.main != null` before the exact same `ScreenToWorldPoint` pattern the Sniper's click handling
uses). An unguarded null dereference inside a Harmony-postfix-driven per-frame handler doesn't crash
the game — it just aborts that one method silently, which can look like "an action sometimes does
nothing, no pattern I can find" if the abort happens before whatever state change would normally follow
(see `SniperSnipeButton.HandleAimFrame`, which bails before `Fire()` so the aim window simply stays
open for the next click attempt instead of consuming this one). Don't assume `Camera.main` is safe to
dereference directly in per-frame code; check for null first, matching existing precedent.

## Wall shadows are HudManager.ShadowQuad, not the light radius

`ShipStatus.CalculateLightRadius` and `LightSource.viewDistance` only size the darkness circle (the
radial fade); the actual wall-occlusion overlay is `HudManager.Instance.ShadowQuad` (a MeshRenderer).
It's toggled via `.gameObject.SetActive(...)`. The vanilla restore rule (from TOU-Mira
`HudManagerPatches.cs:99`): `SetActive(!PlayerControl.LocalPlayer.Data.IsDead)` — shadows on for the
living, off for ghosts and spectators. TOU-Mira precedents: `MedSpiritObject.cs:129/177`,
`SpectatorRole.cs`. When a modifier needs to peek through walls (Sniper aiming, MedSpirit healing,
Spectator watching), disable the shadow quad while active and self-heal it every rendered frame per
the house doctrine (vanilla animations and other patches may flip it back).

## One throwing Harmony postfix skips the rest of the chain

When a postfix on a shared hot method (e.g. `HudManager.Update`) throws an exception, Harmony aborts
the remaining postfixes for that invocation. For per-frame input handlers (clicks, typed keys), this
silently eats the input with no visible pattern — the click or keypress simply never reaches the later
patches. Defense: declare the postfix at `[HarmonyPriority(Priority.First)]` to run before other
mods' patches, and wrap your own handler in try/catch (log via the global Reactor logger) so you never
break theirs either. See `Patches/SniperAimPatch.cs` for the pattern.

## Incapacitating a player: use TOU-Mira's `DisabledModifier`, not manual button fiddling

One-shot `HudManager.Instance.ReportButton.SetDisabled()` gets re-enabled by vanilla the next time
its state refreshes. The house pattern is a `DisabledModifier` subclass (`CanReport`,
`CanUseAbilities`, `CanUseConsoles`, `CanOpenMap`, `CanBeInteractedWith`) — TOU-Mira's
`ButtonClickPatches` and targeting utilities consume it, so it also makes the player untargetable by
kill/ability buttons. Pair with Ambusher's freeze for movement: owner-only `moveable = false` +
`MyPhysics.ResetMoveState()` + `NetTransform.SetPaused(true)`. See `DevouredDisabledModifier`.

## Button singletons persist for the whole game process — stale ability state must self-heal

MiraAPI creates ONE instance of each `CustomActionButton` per process; leaving a lobby or freeplay
session does not reset its private fields. If an ability-end path throws before finishing its restore,
a stuck state flag (e.g. `driveLockActive`) plus a `FixedUpdate` self-heal that keeps re-asserting
`moveable = false` while that flag is set will freeze the player from tick one of every later game
with that role — greying out ALL ability buttons, since `TownOfUsButton.CanUse()` gates on `CanMove`.
Two rules:

1. In ability-end paths, restore player state (movement, camera, light) BEFORE doing anything that
   can throw — RPC sends, object destruction. An exception after the restore is an inconvenience; an
   exception before it is a soft-locked game.
2. Treat "state flag set but its effect/world object is gone" as stale and recover in `FixedUpdate`,
   rather than trusting that every exit path ran. See `RcXdDeployButton.FixedUpdate` for the pattern.

Related: parenting the local player's `lightSource` to a spawned object means destroying that object
destroys the light — a permanent black screen. Reparent the light back before destroying such an
object (see `RcXdCar.DestroyLocally`), and reset `localPosition` to zero afterward, since reparenting
keeps the WORLD position.

## Role-gated buttons stop ticking the moment the player dies

MiraAPI drives every button from a `PlayerControl.FixedUpdate` postfix, but only while
`button.Enabled(role)` is true — and `TownOfUsRoleButton<TRole>.Enabled` requires
`role is TRole`. Dying swaps `Data.Role` to a ghost role (vanilla `RoleManager.AssignRoleOnDeath`,
which both MiraAPI and TOU-Mira keep), so a button's `FixedUpdate` override **silently stops running
on the exact tick the player dies** — no exception, no log. Any cleanup that lives in `FixedUpdate`
(death-cancel paths, effect countdowns, state restores) never fires if the ability itself is what
killed the player, or if they die mid-effect. The RC-XD's self-kill stranded the camera on its blast
anchor this way. Fix: override `Enabled` to also return true while any of the button's own pending
state flags are set, so the framework keeps driving it until cleanup completes — see
`RcXdDeployButton.Enabled` / `SniperSnipeButton.Enabled`. The button stays visually hidden for dead
players regardless (TOU's `SetActive` gates on `!HasDied()`), so this has no HUD side effects.

**Correction (2026-07-18):** an earlier version of this entry claimed `RpcSpecialMultiMurder`
hard-crashes when the source is one of its own targets, and that splitting the self-kill through
`RpcCustomMurder(player, player)` fixed it. That diagnosis was wrong — see "Pinned package versions
silently drifting from the installed mod stack causes native crashes" below for the real cause. A
plain TOU Sheriff misfire (no addon code involved) crashed identically, which `RpcCustomMurder`
couldn't have fixed. The `RcXdCar.RpcDetonateCar` split (multi-murder for other victims,
`RpcCustomMurder` for the deployer) was left in place since it's a reasonable pattern either way —
but don't treat "self-kill via multi-murder crashes" as a proven rule; it wasn't.

**Second correction (2026-07-19):** the version-skew conclusion below did not hold up either — the
crash persisted on the fully version-matched build, including on a plain TOU Sheriff misfire with
the addon merely loaded. See "Local-death native crash in the il2cpp asset-unload path" below for
the current state of evidence.

## Pinned package versions silently drifting from the installed mod stack causes native crashes

`AmongUs.props` pins `Reactor`/`AllOfUs.MiraAPI`/`TownOfUsMira` versions the addon compiles against,
but nothing checks those match what's actually installed in `BepInEx/plugins/` at runtime. They can
drift a long way apart without a single compile error — mismatched managed assemblies still link
fine; only *actually-changed* signatures fail to compile, and most of a mod API surface doesn't
change release to release. The result when they do drift: undefined behavior, not a clean failure.
Diagnosing the RC-XD self-kill crash (see docs/roles/rc-xd.md) burned three build/fix cycles chasing
plausible-looking managed-code causes (RPC call shape, ghost-role timing, event ordering) before
confirming the actual cause was version skew — the addon was compiled against TownOfUsMira
1.5.0-beta.1 / MiraAPI 0.3.5 / Reactor 2.5.0-ci.371 while the installed plugins were 1.6.3-beta2 /
0.4.1 / 2.5.1. The tell that should have been checked first: **the crash was role-agnostic (a plain
TOU Sheriff misfire crashed identically) and disappeared entirely when the addon DLL was removed** —
any bug that global and that tied to "our DLL present or not" is a linkage/version problem before
it's a logic problem.

How to check: `strings BepInEx/plugins/TownOfUsMira.dll | grep -E '^[0-9]+\.[0-9]+\.[0-9]+'` (and
same for `MiraAPI.dll`/`Reactor.dll`) against the versions in `AmongUs.props`. To fix: bump the
pinned versions to match, `dotnet restore --force-evaluate`, then fix whatever actually fails to
compile — in practice this was small (2-3 files) even across a multi-minor-version jump, because
most breakage is a moved/renamed type (e.g. `ModifierFaction` moved from `MiraAPI.Modifiers` to
`TownOfUs.Modifiers` between these versions) rather than a deep behavioral change. Decompile the new
pinned DLL (`ilspycmd`, see the entry above) to find where a moved type landed rather than guessing.
This is also a case where checking for a TOU-Mira update **first**, before deep-diving into RPC
semantics, would have been the faster diagnostic path — worth doing whenever a crash is this
inexplicable from the addon's own code alone.

## Local-death native crash in the il2cpp asset-unload path (open, 2026-07-19)

The recurring "self-kill crashes the game" bug (see docs/roles/rc-xd.md playtest history) is a
native crash, not a managed one, and it is not caused by any murder/death logic in this addon:

- Wine records `Unhandled exception: page fault on read access to 0x000000bd` at
  `gameassembly+0x2eb256` — a byte read at offset 0xBD off a NULL pointer, inside GameAssembly's
  il2cpp *runtime* region (between the last `il2cpp_*` export and the COM stubs, i.e. runtime
  internals, not generated game/mod code), called from UnityPlayer on a worker thread
  (thread-start frames at the stack bottom — likely the async loading/unload thread).
- It fires right after the LOCAL player dies; the last Player.log line is always Unity's
  "Unloading N Unused Serialized files" asset-unload scan. Deaths of other players don't trigger
  it. A local death is also what plays the vanilla KillOverlay stinger and the resulting asset
  churn/unload, which is why it clusters there.
- It reproduces with a plain TOU Sheriff misfire (no addon code in the kill path, no addon roles
  even assigned to dummies) — but per repeated user testing, only when SuperSquadAmongUs.dll is
  loaded. It is non-deterministic: the same RC-XD self-kill succeeded once and crashed later the
  same morning. Any "TOUM-only doesn't crash" control therefore needs MANY runs to mean anything.
- Ruled out: TOU/MiraAPI/Reactor pin skew (informational versions verified matched);
  BepInEx-core skew (the active install's core is byte-identical in version to the official
  TOU-Mira 1.6.2 pack: BepInEx 6.0.0-be.752, Il2CppInterop.Runtime 1.5.0-ci.620); managed
  exceptions in addon event handlers or Harmony patches (MiraAPI catches event-handler
  exceptions; our hot patches are try/caught or role-gated; nothing is logged at crash time even
  with instant flushing).

Diagnostics for the next occurrence:

- `InstantFlushing = true` is set in the toum install's `BepInEx/config/BepInEx.cfg`
  (`[Logging.Disk]`) so the BepInEx log tail survives hard crashes. Revert when done (tiny I/O
  cost).
- The Steam launch options include `PROTON_LOG=1`, so every crash writes a Wine backtrace to
  `~/steam-945360.log` — but it is OVERWRITTEN on every launch; copy it out after each crash.
  Compare the faulting `gameassembly+0x......` offsets across crashes: a stable offset means one
  deterministic runtime fault site (likely the liveness/unload scan walking some object of ours);
  scattered offsets would mean heap corruption instead.
- Related pitfall fixed during this investigation: MiraAPI's `RpcCustomMurder`/`CustomMurder`
  default `teleportMurderer: true`, which makes the kill coroutine yield (blur animation, camera
  lock) instead of completing synchronously. Any design that relies on a death completing
  synchronously (RC-XD's restore-before-detonate) must pass `teleportMurderer: false` explicitly —
  and when a comment claims "no yields with teleportMurderer false", verify the call site actually
  passes false.

**Update (2026-07-19, later):** with `InstantFlushing` on, the flushed log finally captured the
poison at the crash moment — on a plain TOU Sheriff misfire, in a TOUM-only session too:

```
Error with kill animation: Il2CppException: System.MethodAccessException: Attempt to access
method 'IEnumerable<OverlayKillAnimation>.GetEnumerator' on type 'DeadBody[]' failed.
  at Extensions.Random[T](...)  at KillOverlay.ShowKillAnimation(killer, victim)  at CustomMurder
```

Vanilla `KillOverlay.ShowKillAnimation` (AU 2026.6.5) is broken when killer == victim: it ends up
enumerating a `DeadBody[]` where it expects a kill-animation array and throws mid-way through
il2cpp generic-class initialization. MiraAPI catches the exception (so nothing crashes right
there, in any configuration), but the aborted native class-init is the prime suspect for the
half-initialized metadata the post-death unload scan later trips over — which would explain the
non-determinism and why TOUM-only sessions usually survive (fewer types/objects in the scan)
while addon sessions usually don't. Mitigation shipped: `Patches/SelfKillOverlayPatch.cs`
prefix-skips the overlay whenever killer == victim (a pairing vanilla gameplay never produces),
and the addon's own self-kill call sites (`RcXdCar`, `AstralFormModifier`) additionally pass
`showKillAnim: false`. If crashes on local self-kill deaths persist with this in place, the
"Error with kill animation" line should be GONE from the flushed log — if it still appears, some
other murder path is reaching the vanilla overlay with killer == victim; if it's gone and the
crash remains, the poison theory is wrong and the next lead is comparing faulting
`gameassembly+0x…` offsets across Proton crash logs.
