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

Files: `Roles/Neutral/GooperRole.cs`, `Buttons/Neutral/GooperGoopButton.cs`,
`Buttons/Neutral/GooperVestButton.cs`, `Buttons/Neutral/GooperKillButton.cs`,
`Buttons/Neutral/GooperSnipeButton.cs`, `Buttons/Neutral/GooperSwoopButton.cs`,
`Modifiers/GooperVestModifier.cs`, `Modifiers/GooperSwoopModifier.cs`, `Events/GooperEvents.cs`,
`Options/Roles/Neutral/GooperOptions.cs`, `Modules/SuperSquadGooper.cs`. Shared foundation (also used by
Kirby, `docs/roles/kirby.md`): `Modules/AbilityGrants.cs`, `Buttons/GrantedAbilityButtons.cs`,
`Modifiers/GrantedSwoopModifierBase.cs`.

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
`Vest` flag; `GooperVestButton` (gated on that flag, click-only — see keybinds below) is what the Gooper
presses to actually pop the vest, adding `GooperVestModifier` (a timed `BaseShieldModifier` copying
`GuardianAngelProtectModifier`'s shape that self-expires after `VestDuration`, then the button's
`VestCooldown` gates the next use; `CanUse` blocks re-popping while one is already active).
`BaseShieldModifier` alone is not universally respected — only the handful of TOU-Mira kill sources that
explicitly check `HasModifier<BaseShieldModifier>()` honor it, and a plain vanilla Impostor kill wouldn't
— so `GooperEvents` hooks `MiraButtonClickEvent`/`BeforeMurderEvent` (copied from `ElusiveEvents`'s
interception pattern, but cancel-only, no teleport) to make the vest block everything while worn.

**2nd goop — permanent kill + vent**, via the shared ability-grant architecture below. **Vent note:**
`Configuration.CanUseVent` reads the `Vent` flag live (drives both the vent button's visibility and
`Vent.CanUse`), but MiraAPI's `CustomRoleManager` bakes the vanilla `RoleBehaviour.CanVent` bool once at
role setup — so `RpcGoop` also sets `gooper.CanVent = true` when granting, keeping every cached-bool vent
path in step. (Kirby does the same when it inherits `Vent`.)

**Keybind allocation.** A fully-powered Gooper holds up to five simultaneous ability buttons, more than
there are distinct action keybinds — and MiraAPI fires *every* enabled button bound to a pressed key, so
non-mutually-exclusive abilities must not share one. Allocation: Kill → `PrimaryAction` (it's the kill
keybind), Goop → `SecondaryAction`, Snipe → `TertiaryAction`, Swoop → `ModifierAction`, Vest → no keybind
(click-only, the overflow slot). Kirby uses the same scheme shifted by one (Swallow owns `PrimaryAction`,
so its Kill moves to `SecondaryAction`).

## Shared ability-grant architecture (Gooper + Kirby)

Both roles need to "grow their kit at runtime" — Gooper via goop tiers, Kirby via digesting other roles
(`docs/roles/kirby.md`). One shared module, not two bespoke systems:

- **`GrantableAbility`** (`Modules/AbilityGrants.cs`) — a `[Flags] enum` (`Kill`, `Vent`, `Snipe`,
  `Swoop`) naming everything a role can dynamically gain. **`IAbilityGrantHolder`** exposes a plain
  mutable `UnlockedAbilities` property, mutated directly inside an already-deterministic RPC/event
  handler on every client (same pattern `VultureRole.EatenBodies++` uses) — no extra sync RPC for the
  flags themselves.
- **`CanUseVent`** reads the `Vent` flag live in `Configuration` — MiraAPI/TOU-Mira read
  `Configuration.CanUseVent` fresh on every access, so venting turns on the instant the flag is set, no
  role-swap needed.
- **Button gating**: one shared generic abstract base per ability
  (`GrantedKillButtonBase<TRole>`/`GrantedSnipeButtonBase<TRole>`/`GrantedSwoopButtonBase<TRole,TModifier>`,
  all in `Buttons/GrantedAbilityButtons.cs`) holds all real targeting/click logic, with `Enabled`
  overridden to also require the relevant flag (`RcXdDeployButton.Enabled`'s stay-registered-but-hidden
  technique — MiraAPI's `TownOfUsButton.SetActive` already hides/disables on a false `Enabled`, no patch
  needed). Each role then gets a ~15-line sealed subclass per ability overriding only
  `Name`/`Sprite`/`Cooldown`/`TextOutlineColor` (`GooperKillButton`, `GooperSnipeButton`,
  `GooperSwoopButton`). Growing the pool later costs one shared base once, then one thin subclass per
  (role, ability) pair — not a full reimplementation each time.
- **Snipe** reuses `Modules/SniperShots.cs` directly (already documented there as reusable,
  role-agnostic machinery). One easy-to-forget extra step per role: `Patches/SniperAimPatch.cs`'s
  `HudManager.Update` postfix needs an added line calling that role's snipe button singleton's
  `HandleAimFrame()` — per-rendered-frame click polling can't be button-instance-driven (see
  "mouse clicks must be polled per rendered frame" in `docs/il2cpp-gotchas.md`). Already wired for
  `GooperSnipeButton`/`KirbySnipeButton`; a third role would need the same line added.
- **Swoop needs its own modifier pair, not TOU-Mira's `SwoopModifier` reused directly** — that class
  hard-codes `CustomButtonSingleton<SwooperSwoopButton>` calls in `OnActivate`/`OnDeactivate` to flip the
  *Swooper's own* button sprite; reusing it as-is would cross-wire into the actual Swooper role. Same
  doctrine `CarriedModifier`'s siblings already follow: shared *shape*, not shared *class*.
  `GrantedSwoopModifierBase` (`Modifiers/GrantedSwoopModifierBase.cs`) holds the shared
  appearance/self-heal logic with a `protected abstract void UpdateButtonVisual(bool swooped);` hook;
  `GooperSwoopModifier`/`KirbySwoopModifier` are sealed one-method siblings implementing it against
  their own button singleton. **The base is `AutoStart => false`, deliberately**: `ConcealedModifier`
  defaults `Duration => 1f`, so an auto-started timer would silently drop the concealment after one
  second (this was a real bug). The granting button owns the timing via its `EffectDuration` and removes
  the modifier in `OnEffectEnd`, exactly how TOU-Mira's `SwoopModifier` is driven by `SwooperSwoopButton`.
- **`AbilityGrants.GetPortableAbilities(RoleBehaviour victimRole)`** — Kirby's curated "what can I
  inherit from this victim" lookup (see `docs/roles/kirby.md` for the full reasoning): a digested
  Gooper/Kirby hands over its `UnlockedAbilities` wholesale; otherwise a small hand-maintained table
  (`CanVent` → `Vent`, `ICustomRole.Configuration.UseVanillaKillButton` → `Kill`, `SniperRole`/`SwooperRole`
  type checks → `Snipe`/`Swoop`). Deliberately **not** a generic reflection-based role clone — the only
  "become like another role" primitive anywhere in MiraAPI/TOU-Mira is `ChangeRole`
  (`TownOfUs/Utilities/Extensions.cs`), a full teardown/reinstantiate of the whole role, not a
  "copy some behavior" primitive.

## Design decisions

- **Win condition: last one standing** (confirmed with the user), not a Vulture-style instant threshold
  win on goop count — Gooper eventually gains a permanent kill button, so an instant-win-on-count design
  sits oddly alongside actual combat power.
- **Puppeteer's control ability is deliberately deferred**, not in the v1 pool (confirmed with the
  user). TOU-Mira's Puppeteer control is a full remote-control subsystem (per-client control-state
  dictionaries, a movement-hijacking Harmony patch, camera/light sync, disconnect handling) — bigger
  than the rest of this whole feature combined. The pool is `[GrantableAbility.Snipe, GrantableAbility.Swoop]`
  for now; add Control later as a separately-scoped follow-up (new flag + pool entry + a portable control
  subsystem).
- **Pool draws are without replacement** (confirmed with the user) — each goop from the 3rd onward
  grants a *new* ability until the pool is exhausted; once both pool abilities are unlocked, further
  goops still mark the body (can't be re-gooped) but grant nothing further.
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
