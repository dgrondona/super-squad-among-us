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
