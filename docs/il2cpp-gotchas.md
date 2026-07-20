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
can't double as the action click in the same frame. Two keybind variants of the same family:

- A single physical keypress can dispatch to a button's ClickHandler more than once (same-frame
  double dispatch, or key autorepeat - observed under Proton), so a two-phase button
  (Deploy/Detonate on one key) needs a short time-based arming delay, not just a frame check - see
  `RcXdDeployButton.DetonateArmDelay`.
- `Keybinds.SecondaryAction` (the standard role-ability key most `TownOfUsRoleButton`s bind to) IS
  the vanilla `AbilityButton`, and `AbilityButton.DoClick()` polls that key independently each
  frame regardless of what else already handled the press. If a custom ability kills the local
  player synchronously, `Data.Role` flips to a ghost role before that poll runs, so the same
  still-down key also fires the ghost's ability (Haunt) later the same frame. Fix: a Harmony
  prefix on `AbilityButton.DoClick` that skips the click for the exact frame the self-kill
  happened - see `Patches/SuppressHauntAfterSelfDetonatePatch.cs` /
  `RcXdCar.SelfDetonationFrame`.

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

## A two-phase *targeted* button's second phase is blocked by `CanClick()`, not just `CanUse()`

`CustomActionButton<T>.CanClick()` is `base.CanClick() && Target != null`, and `base.CanClick()` is
`(EffectActive ? IsEffectCancellable() : Timer <= 0) && CanUse()`. So a targeted button
(`TownOfUsRoleButton<TRole, TTarget>`) can only be clicked when there is a fresh valid `Target` AND the
cooldown has elapsed — **overriding `CanUse()` is not enough**, because `ClickHandler → CanClick()`
re-enforces both gates independently. This silently breaks any "pick up / put down" or "attach /
detonate" toggle where the second press happens while the pickup cooldown is still running and/or with
no valid target under the cursor (Dumper's early drop, Detonator's detonate — both were dead on arrival
until fixed). Fix: override `ClickHandler`, detect the second-phase state (a held modifier / a tracked
`activeBomb`), and run that branch gated on `CanUse()` alone (fold the alive/hacked/`DisabledModifier`
guards into `CanUse`), bypassing `CanClick()` entirely. See `DumperCarryButton.ClickHandler` /
`DetonatorAttachButton.ClickHandler`. (The Undertaker sidesteps this differently — it keeps the dragged
body a valid nearby `Target` and never starts the cooldown until drop — but that only works because the
body stays visible and in range; a hidden/teleported body can't.)

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

(Two earlier corrections to this entry chased wrong diagnoses — `RpcSpecialMultiMurder` self-target
crashes, then package version skew — before landing on the real cause below; see git history if the
detour itself is useful context.)

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

## Local-death native crash — upstream TOU-Mira/AU 2026.6.5 memory corruption, not addon-fixable (open, tracked upstream)

The recurring "self-kill crashes the game" bug (see docs/roles/rc-xd.md playtest history) is a
native page fault inside GameAssembly's il2cpp runtime (`gameassembly+0x2eb256`, a byte read at
offset 0xBD off a NULL pointer), on a worker thread, always shortly after a LOCAL player's death.
**As of 2026-07-20, this is confirmed NOT fixable from this addon.**

Two addon-side theories were tried and both falsified by re-testing with the same crash signature
still occurring:

1. Vanilla `KillOverlay.ShowKillAnimation` throwing for killer == victim (mitigated by
   `Patches/SelfKillOverlayPatch.cs` + `showKillAnim: false` at self-kill call sites) - the crash
   recurred with the exact same fault address even though the log confirmed this exception no
   longer fires. Kept the patch anyway (it fixes a real, separate, confirmed-broken vanilla method
   for the self-kill overlay itself), but it is not the crash's cause.
2. MiraAPI's `DeepDestroy()`/`Resources.UnloadUnusedAssets()` forced-GC path, gated on AU version
   ≥ 2026.6.5 (`MainMenuManagerPatches.NeedsDeepDestroy`) - ruled out: the "Unloading N Unused
   Serialized files" log line it produces also fires repeatedly during main-menu/login, well before
   any players or deaths exist, so it's Unity's own routine periodic asset scan, not something
   `DeepDestroy` uniquely triggers around death.

**Root cause, per TOU-Mira's own maintainer:** the 1.6.3-beta2 changelog (the release this repo is
pinned to, and the latest available - `1.6.3` on GitHub is tagged *older* than `1.6.3-beta.1`/
`-beta2` and marked "Outdated - Do Not Use") lists as a known bug: "Memory corruption breaks kill
animations, body pop-ups, and a few more things at times." A TOU-Mira maintainer (Nix-main) on
[AU-Avengers/TOU-Mira#197](https://github.com/AU-Avengers/TOU-Mira/issues/197) confirms: "This is
likely caused by the various unavoidable stability issues with the latest version of Among Us. We
are working to address these problems but we will likely have to wait until the next update. It's
recommended to stay on the previous version for now." This matches every symptom observed here:
non-deterministic, clusters around kill/death processing, reproduces on a plain TOU Sheriff misfire
with zero addon code involved, and is *amplified* (not caused) by this addon simply because more
registered types/objects raise the odds of the corruption being hit during any given scan.

There is no known addon-side fix for upstream memory corruption. If this needs to stop happening
now rather than waiting for a TOU-Mira update, the only currently-confirmed mitigation is
downgrading the installed Among Us version below 2026.6.5 (per the maintainer's own advice) - a
game/launcher-level change, not something this repo controls. Re-check
[AU-Avengers/TOU-Mira releases](https://github.com/AU-Avengers/TOU-Mira/releases) periodically for
a build past 1.6.3-beta2; if the changelog claims this fixed, bump the pin (`AmongUs.props`) *and*
the installed plugin DLL together, per the version-skew entry above.

Related pitfall fixed along the way: MiraAPI's `RpcCustomMurder`/`CustomMurder` default
`teleportMurderer: true`, which makes the kill coroutine yield (blur animation, camera lock) instead
of completing synchronously - any design that relies on a death completing synchronously (RC-XD's
restore-before-detonate) must pass `teleportMurderer: false` explicitly. This is unrelated to the
crash above but was found while investigating it.
