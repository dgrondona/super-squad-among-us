# Gooper

Neutral Killing role. "Goop" dead bodies with the primary ability button — the body is **not**
destroyed (it stays on the map, reportable/cleanable by other roles), just marked so it can't be gooped
again. An arrow points toward every un-gooped body while alive, mirroring the Vulture's eat button. Each
successful goop grants an escalating power: the 1st goop unlocks a **Vest ability** (a button — press it
to become briefly unkillable, then it goes on cooldown); the 2nd gives a permanent kill button and the
ability to vent; the 3rd and every goop after grants one additional ability, drawn without repeats from a
pool (currently Sniper's snipe and Swooper's concealment — see "Shared ability-grant architecture"
below). Wins by outlasting the opposition once fully powered up, the same "last one standing" shape as
`SentinelRole`.

Gooper-specific files: `Roles/Neutral/GooperRole.cs`, `Buttons/Neutral/GooperGoopButton.cs`,
`Events/GooperEvents.cs`, `Options/Roles/Neutral/GooperOptions.cs`, `Modules/SuperSquadGooper.cs`.
Shared, **role-agnostic** granted-ability foundation (also used by Kirby, `docs/roles/kirby.md`):
`Modules/AbilityGrants.cs`, `Buttons/GrantedAbilityButtons.cs` (the single `Granted*Button` per ability),
`Modifiers/GrantedSwoopModifier.cs`, `Modifiers/GrantedVestModifier.cs`, `Modifiers/CloakHiddenModifier.cs`
(Hide, shared with Daddy Hagrid), `Options/GrantedAbilityOptions.cs`.

## How it works

**Goop button** is a direct copy of `VultureEatButton`'s shape (per-body arrow via
`MiscUtils.CreateArrow`, synced every `FixedUpdate`, cleared on death/meeting/option-off), except
`OnClick` never calls `SuperSquadBodies.DestroyBodies` — the body's `ParentId` is just added to
`GooperRole.GoopedBodyIds : HashSet<byte>`, and `IsTargetValid`/the arrow sync both skip any id already
in that set.

**Tier escalation is one RPC**, since it branches on goop count and can't be a single generic modifier
add: `SuperSquadGooper.RpcGoop(source, bodyId, chosenPoolAbility)` no-ops if `bodyId` is already gooped,
otherwise switches on `GoopedBodyIds.Count` after adding: 1 → `UnlockedAbilities |= Vest`; 2 →
`UnlockedAbilities |= Kill | Vent` (and sets the vanilla `CanVent` bool, see the vent note below);
default (3+) → `UnlockedAbilities |= chosenPoolAbility`. Every tier is now just a flag flip — no
per-client modifier add. `chosenPoolAbility` is picked client-side in `GooperGoopButton.OnClick()` via
`AbilityGrants.PickRandomPoolAbility` *before* the RPC is sent, so every client applies the identical
draw (same split `ElusiveEvents` uses for its teleport destination) — passed as a `byte` cast of the
`GrantableAbility` flag, since Reactor RPC parameters need to be simple serializable types.

**1st goop — the Vest is an unlockable *ability*, not an auto-applied shield.** The goop only sets the
`Vest` flag; the shared `GrantedVestButton` (gated on that flag, click-only — see keybinds below) is what
the Gooper presses to actually pop the vest, adding `GrantedVestModifier` (a timed `BaseShieldModifier`
copying `GuardianAngelProtectModifier`'s shape that self-expires after `GrantedAbilityOptions.VestDuration`,
then the button's `VestCooldown` gates the next use; `CanUse` blocks re-popping while one is already active).
`BaseShieldModifier` alone is not universally respected — only the handful of TOU-Mira kill sources that
explicitly check `HasModifier<BaseShieldModifier>()` honor it, and a plain vanilla Impostor kill wouldn't
— so `GooperEvents` hooks `MiraButtonClickEvent`/`BeforeMurderEvent` (copied from `ElusiveEvents`'s
interception pattern, but cancel-only, no teleport) to make the vest block everything while worn.

**2nd goop — permanent kill + vent**, via the shared ability-grant architecture below. **Vent note:**
`Configuration.CanUseVent` reads the `Vent` flag live (drives both the vent button's visibility and
`Vent.CanUse`), but MiraAPI's `CustomRoleManager` bakes the vanilla `RoleBehaviour.CanVent` bool once at
role setup — so `RpcGoop` also sets `gooper.CanVent = true` when granting, keeping every cached-bool vent
path in step. (Kirby does the same when it inherits `Vent`.)

**Keybind allocation.** A fully-powered Gooper holds up to six simultaneous ability buttons, more than
there are distinct action keybinds — and MiraAPI fires *every* enabled button bound to a pressed key, so
non-mutually-exclusive abilities must not share one. Allocation: Kill → `PrimaryAction` (it's the kill
keybind), Goop → `SecondaryAction`, Snipe → `TertiaryAction`, Swoop → `ModifierAction`, Vest and Hide →
no keybind (click-only, the overflow slots). Kirby uses the same scheme (Swallow takes `SecondaryAction`
too, since the granted Kill owns `PrimaryAction`).

## Shared ability-grant architecture (Gooper + Kirby)

Both roles need to "grow their kit at runtime" — Gooper via goop tiers, Kirby via swallowing other roles
(`docs/roles/kirby.md`). One shared, **role-agnostic** system — not per-role reimplementations:

- **`GrantableAbility`** (`Modules/AbilityGrants.cs`) — a `[Flags] enum` (`Kill`, `Vent`, `Snipe`,
  `Swoop`, `Vest`, `Hide`) naming everything a role can dynamically gain. **`IAbilityGrantHolder`**
  exposes a plain mutable `UnlockedAbilities` property, mutated directly inside an already-deterministic
  RPC/event handler on every client (same local-mutation pattern the Vulture's eat count uses) — no extra
  sync RPC for the flags themselves.
- **One button per ability, shown for ANY role that unlocked its flag — no per-(role, ability)
  subclass.** This is the key change from the first cut. `Buttons/GrantedAbilityButtons.cs` holds a
  single concrete button per ability (`GrantedKillButton`, `GrantedSnipeButton`, `GrantedSwoopButton`,
  `GrantedVestButton`, `GrantedHideButton`), each extending `TownOfUsButton`/`TownOfUsTargetButton<PlayerControl>`
  **directly** (not `TownOfUsRoleButton<TRole>`) and gating `Enabled` on
  `role is IAbilityGrantHolder h && h.UnlockedAbilities.HasFlag(X)` instead of a role type — the same
  modifier-gated-button pattern TOU-Mira's own `ScientistButton` uses. A single client is only ever one
  role, so the one button singleton serves whichever granting role the local player is. Adding a new
  ability now costs exactly **one** button here (+ a flag, a modifier if needed, and one
  `GetPortableAbilities` entry) — never N per-role copies. Targeted buttons share
  `GrantedTargetButtonBase`, which re-supplies the player-outline / target-validity bits
  `TownOfUsRoleButton<TRole, TTarget>` would otherwise provide.
- **Shared options.** Because the buttons have no single owning role, their cooldowns/durations live in a
  standalone `Options/GrantedAbilityOptions.cs` (`AbstractOptionGroup`, like TOU-Mira's own
  `VanillaTweakOptions`), read as `OptionGroupSingleton<GrantedAbilityOptions>.Instance.X.Value` — a
  granted Snipe is the same Snipe no matter who unlocked it.
- **Venting** turns on via `AbilityGrants.EnableVenting(role)`: `Configuration.CanUseVent` reads the
  `Vent` flag live (drives `Vent.CanUse`), but MiraAPI bakes the vanilla `RoleBehaviour.CanVent` bool
  once at role setup, and the on-screen `ImpostorVentButton`'s visibility is only applied inside
  `HudManager.SetHudActive` (not per frame) — so `EnableVenting` sets `CanVent = true` AND re-runs
  `SetHudActive` on the owner's client to surface the button the instant it's unlocked.
- **Snipe** reuses `Modules/SniperShots.cs` directly (role-agnostic hit math). The single
  `GrantedSnipeButton` needs exactly one line in `Patches/SniperAimPatch.cs`'s `HudManager.Update`
  postfix (per-rendered-frame click polling can't be button-instance-driven — see "mouse clicks must be
  polled per rendered frame" in `docs/il2cpp-gotchas.md`); because the button is role-agnostic, growing
  the roster never needs another line.
- **Swoop reuses a sibling of TOU-Mira's `SwoopModifier`, not that class directly** — it hard-codes
  `CustomButtonSingleton<SwooperSwoopButton>` in `OnActivate`/`OnDeactivate` and would cross-wire the
  real Swooper. `Modifiers/GrantedSwoopModifier.cs` is one role-agnostic sibling (paired with the single
  `GrantedSwoopButton`). **It's `AutoStart => false`, deliberately**: `ConcealedModifier` defaults
  `Duration => 1f`, so an auto-started timer silently drops the concealment after one second (this was a
  real bug); the button owns the timing via its `EffectDuration` and removes the modifier in
  `OnEffectEnd`, exactly how `SwooperSwoopButton` drives `SwoopModifier`.
- **Hide reuses Daddy Hagrid's ability verbatim.** `GrantedHideButton` adds this repo's own
  `CloakHiddenModifier` (a `CarriedModifier`) to the target — literally the Daddy Hagrid ability, not a
  copy. Its release-on-caster-death/disconnect lives in `Events/DaddyHagridEvents.cs`, which was
  generalized to key on the modifier's own `Carrier` (not the carrier being a DaddyHagrid) so a granted
  Hide releases correctly too. Duration comes from `DaddyHagridOptions.HideDuration` (it *is* Hagrid's
  ability); only its cooldown is a `GrantedAbilityOptions` value.
- **`AbilityGrants.GetPortableAbilities(RoleBehaviour victimRole)`** — Kirby's curated "what can I
  inherit from this victim" lookup (see `docs/roles/kirby.md`): a swallowed Gooper/Kirby hands over its
  `UnlockedAbilities` wholesale; otherwise a small hand-maintained table (`CanVent` → `Vent`,
  `ICustomRole.Configuration.UseVanillaKillButton` → `Kill`, `SniperRole`/`SwooperRole`/`DaddyHagridRole`
  type checks → `Snipe`/`Swoop`/`Hide`). **This table is the one place to grow** when a new ability is
  added. Deliberately **not** a generic reflection-based role clone — the only "become like another role"
  primitive anywhere in MiraAPI/TOU-Mira is `ChangeRole` (`TownOfUs/Utilities/Extensions.cs`), a full
  teardown/reinstantiate, not a "copy one ability" primitive. Every transferable ability must therefore
  be implemented once as a granted button here; the framework just makes that once-per-ability, not
  once-per-(role, ability), and makes it work for every current and future granting role for free.

## Design decisions

- **Win condition: last one standing** (confirmed with the user), not a Vulture-style instant threshold
  win on goop count — Gooper eventually gains a permanent kill button, so an instant-win-on-count design
  sits oddly alongside actual combat power.
- **Adding a new pool ability is a one-file job** thanks to the role-agnostic framework above: one flag,
  one `Granted*Button` (+ modifier if the effect needs one), one `GetPortableAbilities` entry, one pool
  entry. The current pool is `[Snipe, Swoop, Hide]`. **Puppeteer's control is still deferred** — not
  because the framework can't hold it, but because the *ability itself* is a whole remote-control
  subsystem in TOU-Mira (per-client control-state dictionaries, a movement-hijacking Harmony patch,
  camera/light sync, disconnect handling). Building `GrantedControlButton` + a portable control subsystem
  is the separately-scoped follow-up; once it exists, dropping it in the pool is trivial.
- **Pool draws are without replacement** (confirmed with the user) — each goop from the 3rd onward
  grants a *new* ability until the pool is exhausted; once every pool ability is unlocked, further goops
  still mark the body (can't be re-gooped) but grant nothing further.
- **Vest only blocks player-attributed murders** (`BeforeMurderEvent` scope), not sabotage or other
  death causes — matching Elusive/Medic's own scope.
- **No cap on total goops.** Escalation continues as long as un-gooped bodies exist; the pool simply
  stops granting anything new once exhausted.

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed — untestable solo (needs bodies to goop and ideally a second
  player). Highest-value checks: goop 3+ bodies in sequence and confirm vest → kill+vent → one pool
  ability unlock in that order; a 4th goop grants the *other* pool ability, not a repeat; the vest
  actually blocks a plain vanilla Impostor kill (not just TOU-Mira sources that check
  `BaseShieldModifier`); arrows only point at un-gooped bodies and disappear once gooped.
- Role icon and every ability button sprite are placeholder art
  (`SuperSquadAssets.NeutralPlaceholderIcon`/`NeutralPlaceholderButton`).
- `AbilityGrants.GetPortableAbilities`'s kill-capability check
  (`ICustomRole.Configuration.UseVanillaKillButton`) only covers this addon's own custom roles — a
  vanilla base-game role with no `ICustomRole` configuration wouldn't be detected as kill-capable if
  ever swallowed by Kirby. Accepted gap, see `docs/roles/kirby.md`.
