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
can't double as the action click in the same frame.

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

## MiraAPI vanilla events fire on every client — sync via local state changes, not host gating

`StartMeetingEvent`, `EjectionEvent`, and `PlayerDeathEvent` are invoked from postfixes on
`MeetingHud`, `ExileController.Begin`, and `PlayerControl.Die`, all of which run on all clients.
TOU-Mira's own handlers (e.g. `LoverEvents`) therefore apply kills with *local* calls
(`DeathHandlerModifier.UpdateDeathHandlerImmediate` + vanilla `player.Exiled()`) ungated — every
client performs the same deterministic change on its own copy. Do not add `AmHost` gates or RPCs in
these handlers; that's TOR's model, not TOU-Mira's. Deciding *which* client acts is only needed for
client-authoritative things like movement (`RpcSnapTo` from the owner).

## Incapacitating a player: use TOU-Mira's `DisabledModifier`, not manual button fiddling

One-shot `HudManager.Instance.ReportButton.SetDisabled()` gets re-enabled by vanilla the next time
its state refreshes. The house pattern is a `DisabledModifier` subclass (`CanReport`,
`CanUseAbilities`, `CanUseConsoles`, `CanOpenMap`, `CanBeInteractedWith`) — TOU-Mira's
`ButtonClickPatches` and targeting utilities consume it, so it also makes the player untargetable by
kill/ability buttons. Pair with Ambusher's freeze for movement: owner-only `moveable = false` +
`MyPhysics.ResetMoveState()` + `NetTransform.SetPaused(true)`. See `DevouredDisabledModifier`.
