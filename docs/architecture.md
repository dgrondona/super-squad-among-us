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

## Physical obstacle / placement checks

**For validating the player's own current position** (placing an object at their feet, e.g. Sentry's
camera or Miner's vent), TOU-Mira's own examples (`SentryPlaceCameraButton`, `MinerPlaceVentButton`)
check an **unmasked** `Physics2D.OverlapBoxAll` there, additionally filtering out `isTrigger` colliders
and specific layer numbers to avoid false positives from themselves/UI/etc. That filtering does not
transfer to a query that already passes a scoped mask like `Constants.ShipAndAllObjectsMask` — copying
it onto an already-masked query can silently exclude the very obstacle colliders you're trying to
detect. When a query is already mask-scoped, don't also filter by `isTrigger`/layer; trust the mask.

**For validating an arbitrary world point that isn't the player's own position** (an "is this clicked
spot walkable" style check), a point/circle overlap test alone is not sufficient, no matter how it's
masked/filtered — a spot on the far side of a thin wall isn't inside any collider, so no point
classifier can see it as blocked (`docs/roles/apparater.md` has the full multi-round story of learning
this the hard way). The reliable approach is **reachability, not classification**: walk a chain of short
steps from a known-good position (a `Physics2D.OverlapCircle` per cell for solid obstacles, plus a
`PhysicsHelpers.AnythingBetween` line-crossing check on each step for thin wall colliders that overlap
checks miss), and only accept destinations connected to that known-good start. `Modules/WalkableRegionSolver.cs`
implements exactly this as a general-purpose utility (`TryFindReachablePoint`) — reach for it before
writing another point-classification check for this kind of problem.

## Per-role notes

Design decisions, current architecture, and known follow-ups for individual roles are tracked in
`docs/roles/<name>.md` — check there before touching an existing role, and add a new file there when you
build the next one.

Keep that file to **current-state information only**, roughly 200-300 lines: what the role does, how it
works now, confirmed design decisions, known follow-ups. If a role accumulates a long round-by-round
bug-fix/investigation history, move it to `docs/roles/<name>-history.md` (see `apparater-history.md` for
the shape of this) with a one-line pointer from the main file — the history is for archaeology (why a
design looks the way it does, what was already tried and rejected), not something to re-read every
session. The same "keep it minimal, push detail into docs" applies to code comments: a short comment
pointing at the relevant doc section beats re-explaining historical context inline.
