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
  also `IWikiDiscoverable` and `IDoomable`, on top of a base role class. `CrewmateRole`/`ImpostorRole`
  are vanilla (Il2Cpp Assembly-CSharp, not this repo), but **`NeutralRole` is TOU-Mira's own class**
  (`TownOfUs/Roles/Classic/Neutral/NeutralKilling/NeutralRole.cs`), which itself derives from
  `RoleBehaviour` and overrides `Deinitialize`/`CanUse`/`SpawnTaskHeader`. That distinction matters on
  a TOU-Mira version bump: the five Neutral roles sit on upstream source that can change between
  releases, while the Crewmate/Impostor ones sit on a vanilla base that only moves with Among Us
  itself. See [il2cpp-gotchas.md](il2cpp-gotchas.md) for the constructor/override quirks this requires.
- `Buttons/<Team>/<X>Button.cs` — abilities extend this addon's `SuperSquadRoleButton<TRole>` (or
  `SuperSquadRoleButton<TRole, TTarget>` for targeted abilities, `SuperSquadKillRoleButton<...>` for
  kill buttons — all in `Buttons/SuperSquadRoleButton.cs`), never TOU-Mira's `TownOfUsRoleButton<TRole>`
  or MiraAPI's raw `CustomActionButton` directly. The bases inherit all of TOU-Mira's
  cooldown/map-based-cooldown/keybind wiring AND make the button borrowable by grant-holding roles
  (Kirby/Gooper) — see "Granting a role abilities it wasn't born with" below for the rules this
  imposes.
- `Options/Roles/<Team>/<X>Options.cs` — `AbstractOptionGroup<TRole>` subclass using
  `[ModdedNumberOption]` / `[ModdedToggleOption]` attributes; read at runtime via
  `OptionGroupSingleton<TOptions>.Instance`.
- Sprites: role icon under `Resources/RoleIcons/`, button sprites under a per-team folder
  (`Resources/NeutButtons/`, `Resources/CrewButtons/`). Referenced from C# via a small static class in
  `Assets/` (`SuperSquadRoleIcons.cs` for role icons; `SuperSquadNeutAssets.cs` / `SuperSquadCrewAssets.cs`
  for buttons/banners) wrapping a `LoadableResourceAsset`. Anything shared across roles (e.g. the mod
  banner) goes in `Assets/SuperSquadAssets.cs`.
- **No final art yet for a role/button?** Don't generate placeholder art. Point that role/button's
  `Assets/` property at `SuperSquadAssets.ImpostorPlaceholderIcon`/`NeutralPlaceholderIcon` (role icons)
  or `ImpostorPlaceholderButton`/`NeutralPlaceholderButton` (button sprites) instead — these wrap Town of
  Us: Mira's own generic team icons (`Resources/Placeholders/{Impostor,Neutral}.png`, copied from
  `reference/TOU-Mira/Images/Icons/`). Role icons and buttons use different `pixelsPerUnit` conventions
  (200 vs. the 100 default), which is why there are separate icon/button properties wrapping the same
  underlying image. See [icon-standards.md](icon-standards.md) for exact target sizes/PPU per asset type.
- Locale strings live in `Resources/Locale/en_US.xml`, using the key prefixes `SuperSquadRole...` and
  `SuperSquadOption...`. Only `en_US.xml` needs the new keys — the other language files in that folder
  are optional translations and are not expected to have every key (missing keys fall back to the
  default string passed to `TouLocale.Get`/`GetParsed`).
- All of `Resources/**/*.*` is embedded automatically via a glob in the `.csproj` — new images/locale
  files need no manual `EmbeddedResource` entry.

`Modules/` holds free-standing gameplay logic used by a role's buttons that isn't itself a
role/button/option (e.g. `Explode.cs` backs Sentinel's explosion ability). `Patches/` holds Harmony
patches applied via the `Harmony.PatchAll()` call in `Plugin.Load()` (e.g. `LogoPatch.cs` swaps the
splash logo to `SuperSquadAssets.Banner`). `Modifiers/` holds MiraAPI modifier classes (auto-registered
by reflection the same way roles/buttons/options are — see "Registration is automatic" above); first
example is `Modifiers/InvisibleBoyModifier.cs`.

## RPC sender validation

Every `[MethodRpc]` handler that exercises a role's ability (not passive lifecycle/cleanup RPCs -
see below) should validate the sender before acting, matching TOU-Mira's own convention (its
Sheriff/Bomber/Cleric/etc. RPCs all do this) — but use the grant-aware helper, not a raw role-type
check, so a role that borrowed the ability kit (see the granting section below) isn't rejected:

```csharp
if (!AbilityGrants.SenderIsOrHolds<YourRole>(source))
{
    Error("RpcYourMethodName - Invalid <role display name>");
    return;
}
```

This catches desync/invalid-state bugs with a clear log line instead of a confusing downstream
failure (or silently doing the wrong thing, like `SuperSquadBodies.RpcVultureEat` used to - it ran
`DestroyBodies` for any sender, only skipping the win-count increment for a non-Vulture). Skip the
guard for RPCs that legitimately fire outside the role's alive/assigned state - e.g.
`RcXdCar.RpcDespawnCar` is called from a death-cancel path after the owner's role has already
swapped to a ghost; see the comments at those call sites for why.

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
build the next one. Universal game modifiers get the same treatment in `docs/modifiers/<name>.md`.

## Granting a role abilities it wasn't born with

If a role needs to dynamically gain abilities at runtime (Gooper's goop-tier escalation, Kirby's
swallow-based inheritance), reuse the granted-ability system — full design in `docs/roles/gooper.md`'s
"Shared ability-grant architecture" section. Two layers:

- **Kits (roles from THIS addon — zero per-ability code).** Every role button here extends the
  grant-aware bases in `Buttons/SuperSquadRoleButton.cs` instead of `TownOfUsRoleButton<TRole>`; their
  `Enabled` passes for the native holder OR any `IAbilityGrantHolder` whose `GrantedKits` contains the
  role type, so the borrower gets the source role's *real* buttons. **This imposes three rules on every
  new role:** (1) buttons extend `SuperSquadRoleButton<TRole>` / `<TRole, TTarget>` /
  `SuperSquadKillRoleButton<TRole, TTarget>`; (2) per-ability mutable state lives on the button
  singleton, never on the role class; (3) ability RPC validators use
  `AbilityGrants.SenderIsOrHolds<TRole>(source)`, and effect-resolving event handlers key on the
  modifier's caster/carrier, not the caster's role type.
- **Flag primitives (`GrantableAbility`) for abilities with no borrowable source button** — vanilla
  Kill/Vent, TOU-Mira-sourced abilities (Swoop), Vest. One `Granted*Button` each in
  `Buttons/GrantedAbilityButtons.cs`, tuned by `Options/GrantedAbilityOptions.cs`.

`AbilityGrants.ApplyPortableGrant` is the single transfer entry point (flags + kit + vent sync,
optional overwrite). **TOU-Mira roles cannot be kit-borrowed** — all 49 of their ability RPC handlers
validate the sender's exact role on every client, 21 of their buttons dereference role state that's
null for a non-holder, and upstream's own Imitator resorts to a full `RpcChangeRole` swap — so a
TOU-Mira ability joins the system by being recreated once as a flag primitive. Do **not** reach for
`ChangeRole` (`TownOfUs/Utilities/Extensions.cs`) — a full teardown/reinstantiate that can't hold
several borrowed abilities at once.

Keep that file to **current-state information only**, roughly 200-300 lines: what the role does, how it
works now, confirmed design decisions, known follow-ups. If a role accumulates a long round-by-round
bug-fix/investigation history, move it to `docs/roles/<name>-history.md` (see `apparater-history.md` for
the shape of this) with a one-line pointer from the main file — the history is for archaeology (why a
design looks the way it does, what was already tried and rejected), not something to re-read every
session. The same "keep it minimal, push detail into docs" applies to code comments: a short comment
pointing at the relevant doc section beats re-explaining historical context inline.
