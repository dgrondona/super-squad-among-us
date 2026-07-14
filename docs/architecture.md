# Addon architecture

How roles/buttons/options are wired up, and the file-layout convention to follow when adding a new one.

## Registration is automatic — there is no manual list

`SuperSquadAmongUsPlugin.Load()` (in `SuperSquadAmongUs/SuperSquadAmongUsPlugin.cs`) only does three
things: registers with `ReactorCredits`, hooks locale loading, and calls `Harmony.PatchAll()`. It does
**not** register roles, buttons, or option groups one by one.

MiraAPI's plugin loader (`MiraPluginManager` in the MiraAPI source under `reference/MiraAPI/`) reflects
over every type in this assembly at load time and auto-registers anything implementing `ICustomRole` +
`RoleBehaviour`, anything deriving from `CustomActionButton`/`CustomActionButton<T>`, and anything
deriving from `AbstractOptionGroup<T>`. Adding a new role is purely additive: create the class files
below and MiraAPI picks them up automatically — no edits to `SuperSquadAmongUsPlugin.cs` needed.

## Per-role file layout

Look at `Apparater` (Crewmate, `Roles/Crewmate/ApparaterRole.cs`) or `Sentinel` (Neutral Killing,
`Roles/Neutral/SentinelRole.cs`, kept around as a second worked reference) as templates. Each role
follows the same shape:

- `Roles/<Team>/<X>Role.cs` — implements `ITownOfUsRole` (TOU-Mira) + `ICustomRole` (MiraAPI), usually
  also `IWikiDiscoverable` and `IDoomable`, on top of the vanilla base game role class (`CrewmateRole`,
  `NeutralRole`, etc. — these live in Il2Cpp Assembly-CSharp, not this repo). See
  [il2cpp-gotchas.md](il2cpp-gotchas.md) for the constructor/override quirks this requires.
- `Buttons/<Team>/<X>Button.cs` — abilities extend `TownOfUs.Buttons.TownOfUsRoleButton<TRole>` (or
  `TownOfUsRoleButton<TRole, TTarget>` for targeted abilities), not MiraAPI's raw `CustomActionButton`
  directly. This base class wires up TOU-Mira's cooldown/map-based-cooldown, keybind icon, and uses
  conventions for you.
- `Options/Roles/<Team>/<X>Options.cs` — `AbstractOptionGroup<TRole>` subclass using
  `[ModdedNumberOption]` / `[ModdedToggleOption]` attributes; read at runtime via
  `OptionGroupSingleton<TOptions>.Instance`.
- Sprites: role icon under `Resources/RoleIcons/`, button sprites under a per-team folder
  (`Resources/NeutButtons/`, `Resources/CrewButtons/`). Referenced from C# via a small static class in
  `Assets/` (`SuperSquadRoleIcons.cs` for role icons; `SuperSquadNeutAssets.cs` / `SuperSquadCrewAssets.cs`
  for buttons/banners) wrapping a `LoadableResourceAsset`. Anything shared across roles (e.g. the mod
  banner) goes in `Assets/SuperSquadAssets.cs`.
- Locale strings live in `Resources/Locale/en_US.xml`, using the key prefixes `SuperSquadRole...` and
  `SuperSquadOption...`. Only `en_US.xml` needs the new keys — the other language files in that folder
  are optional translations and are not expected to have every key (missing keys fall back to the
  default string passed to `TouLocale.Get`/`GetParsed`).
- All of `Resources/**/*.*` is embedded automatically via a glob in the `.csproj` — new images/locale
  files need no manual `EmbeddedResource` entry.

`Modules/` holds free-standing gameplay logic used by a role's buttons that isn't itself a
role/button/option (e.g. `Explode.cs` backs Sentinel's explosion ability). `Patches/` holds Harmony
patches applied via the `Harmony.PatchAll()` call in `Plugin.Load()` (e.g. `LogoPatch.cs` swaps the
splash logo to `SuperSquadAssets.Banner`).

## Colors

Shared role/button colors go in root-level `SuperSquadColors.cs` (and player colors in
`SuperSquadPlayerColors.cs`), following the pattern
`TownOfUsColors.UseBasic ? Palette.CrewmateBlue : new Color32(...)` so custom role colors respect the
"use basic crewmate team color" accessibility setting.

## Per-role notes

Design decisions and known follow-ups for individual roles are tracked in `docs/roles/<name>.md` —
check there before touching an existing role, and add a new file there when you build the next one.
