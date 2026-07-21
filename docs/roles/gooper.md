# Gooper

Neutral Killing role. "Goop" dead bodies with the primary ability button — the body is **not**
destroyed (it stays on the map, reportable/cleanable by other roles), just marked so it can't be gooped
again. An arrow points toward every un-gooped body while alive, mirroring the Vulture's eat button. Each
successful goop grants an escalating power: the 1st goop unlocks a **Vest ability** (a button — press it
to become briefly unkillable, then it goes on cooldown); the 2nd gives a permanent kill button and the
ability to vent; the 3rd and every goop after grants one additional ability, drawn without repeats from a
pool (currently the Sniper's snipe kit, the Swooper's concealment, and Daddy Hagrid's hide kit — see
"Shared ability-grant architecture" below). Wins by outlasting the opposition once fully powered up, the
same "last one standing" shape as `SentinelRole`.

Gooper-specific files: `Roles/Neutral/GooperRole.cs`, `Buttons/Neutral/GooperGoopButton.cs`,
`Events/GooperEvents.cs`, `Options/Roles/Neutral/GooperOptions.cs`, `Modules/SuperSquadGooper.cs`.
Shared granted-ability foundation (also used by Kirby, `docs/roles/kirby.md`):
`Modules/AbilityGrants.cs`, `Buttons/SuperSquadRoleButton.cs` (the grant-aware button bases),
`Buttons/GrantedAbilityButtons.cs` (the primitive `Granted*Button`s: Kill/Swoop/Vest),
`Modifiers/GrantedSwoopModifier.cs`, `Modifiers/GrantedVestModifier.cs`, `Options/GrantedAbilityOptions.cs`.

## How it works

**Goop button** is a direct copy of `VultureEatButton`'s shape (per-body arrow via
`MiscUtils.CreateArrow`, synced every `FixedUpdate`, cleared on death/meeting/option-off), except
`OnClick` never calls `SuperSquadBodies.DestroyBodies` — the body's `ParentId` is just added to
`GooperRole.GoopedBodyIds : HashSet<byte>`, and `IsTargetValid`/the arrow sync both skip any id already
in that set.

**Tier escalation is one RPC**, since it branches on goop count and can't be a single generic modifier
add: `SuperSquadGooper.RpcGoop(source, bodyId, chosenPoolIndex)` no-ops if `bodyId` is already gooped,
otherwise switches on `GoopedBodyIds.Count` after adding: 1 → `UnlockedAbilities |= Vest`; 2 →
`UnlockedAbilities |= Kill | Vent` (plus `AbilityGrants.SyncVenting`, see the vent note below);
default (3+) → `AbilityGrants.ApplyPoolGrant(gooper, chosenPoolIndex)`. The draw is picked client-side
in `GooperGoopButton.OnClick()` via `AbilityGrants.PickRandomPoolIndex` *before* the RPC is sent, so
every client applies the identical result (same split `ElusiveEvents` uses for its teleport
destination) — serialized as a byte **index into `AbilityGrants.Pool`**, since a pool entry can be a
flag, a borrowed button kit, or both (`PortableGrant`), which a raw flag byte can't express.

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

**Keybind allocation.** MiraAPI fires *every* enabled button bound to a pressed key, so
non-mutually-exclusive abilities must not share one. Allocation: granted Kill → `PrimaryAction` (it's
the kill keybind), **Goop and Kirby's Swallow → `TertiaryAction`** (the role's own core ability lives on
the slot borrowed kits rarely use), granted Swoop → `ModifierAction`, Vest → click-only. **Borrowed kit
buttons keep their source role's keybind** — usually `SecondaryAction` (Sniper's snipe, most targeted
abilities) — which is exactly why Goop/Swallow moved off Secondary. Two borrowed kits can still collide
with each other on Secondary (e.g. an accumulate-mode Kirby holding both Snipe and Hex); the mouse
click only ever hits one button, so clicking is the disambiguation fallback. Real keybind arbitration
(suppressing a borrowed button's keybind when another usable button shares it) is a known follow-up —
MiraAPI's keybind handler is a closure registered per button at creation, with no per-source hook to
override.

## Shared ability-grant architecture (Gooper + Kirby)

Both roles need to "grow their kit at runtime" — Gooper via goop tiers, Kirby via swallowing other roles
(`docs/roles/kirby.md`). The system has **two layers**, and the distinction is load-bearing:

**Layer 1 — borrowed button KITS (any role from THIS addon, zero per-ability code).**
`IAbilityGrantHolder.GrantedKits : HashSet<Type>` holds this addon's role types whose *real* buttons the
holder has borrowed. Every role button in this addon extends the grant-aware bases in
`Buttons/SuperSquadRoleButton.cs` (`SuperSquadRoleButton<TRole>` / `<TRole, TTarget>` /
`SuperSquadKillRoleButton<TRole, TTarget>`) instead of TOU-Mira's `TownOfUsRoleButton<TRole>` directly.
The only behavioral difference is `Enabled`: `AbilityGrants.IsOrHolds(role, typeof(TRole))` — true for
the native holder OR any grant-holder whose kit set contains `TRole`. MiraAPI re-evaluates `Enabled`
every frame, so a borrowed button appears the instant the kit is granted. The borrower gets the source
role's REAL button — same code, same options, same cooldowns; nothing is copied. This is what makes a
swallowed Apparater grant real Apparate, a swallowed RC-XD grant the real car, etc., for every current
and future role in this addon automatically. Three rules keep buttons borrowable (all verified by
audit): per-ability mutable state lives on the BUTTON singleton, never on the role class (buttons are
per-client singletons, so they serve whichever role the local player is — see `SuiProtectButton`'s
`protectedTarget` for the worked example of moving it); ability RPC validators use
`AbilityGrants.SenderIsOrHolds<TRole>(source)` instead of `source.Data.Role is not TRole`; and event
handlers that resolve an ability's effects key on the modifier's caster/carrier, not the caster's role
type (`DaddyHagridEvents`, `PelicanEvents`, `WitchEvents` are the generalized examples).

**Layer 2 — flag PRIMITIVES (`GrantableAbility`: `Kill`, `Vent`, `Swoop`, `Vest`).** For abilities with
no borrowable source button in this addon: vanilla Kill/Vent, the TOU-Mira Swooper's Swoop (recreated
once as `GrantedSwoopButton` + `GrantedSwoopModifier`, since TOU-Mira's own `SwoopModifier` hard-codes
`CustomButtonSingleton<SwooperSwoopButton>` — and note the modifier's deliberate `AutoStart => false`:
`ConcealedModifier` defaults `Duration => 1f`, so an auto-started timer silently drops the concealment
after one second, a real past bug), and Gooper's Vest (`GrantedVestButton`, no source role at all).
These primitive buttons gate `Enabled` on the flag (the `ScientistButton` pattern) and read their tuning
from the standalone `Options/GrantedAbilityOptions.cs`. Kit-borrowed buttons use the source role's own
options instead — a borrowed Snipe is tuned by `SniperOptions`, a borrowed Hide by
`DaddyHagridOptions`.

**Why TOU-Mira roles can't be Layer 1** (audited across all 107 TOU-Mira button classes): (a) their
buttons' `Enabled` is `role is TRole` and their `Role` property is
`PlayerControl.LocalPlayer.GetRole<TRole>()!` — 21 of their buttons dereference role state that is null
for a borrower; (b) decisively, **all 49 of their ability RPC handlers validate
`source.Data.Role is not XRole` on every client**, so a borrowed click would apply locally and be
rejected remotely (desync) — and that's their source, not ours to edit; (c) several of their modifiers
hard-reference their own button singletons. TOU-Mira's own Imitator — upstream's only
"use another role's kit" role — solves this with `RpcChangeRole` (a full role swap) plus a
swap-surviving cache modifier; there is no lighter upstream primitive. So a TOU-Mira ability joins this
system by being recreated once as a Layer-2 primitive (like Swoop), prioritized on demand.

**Shared plumbing:**

- **`AbilityGrants.ApplyPortableGrant(recipient, victimRole, overwrite)`** — the single transfer
  function. Victim from this addon → its whole kit (`GrantedKits.Add(type)`, minus the Mafia
  promotion-entangled roles) plus Vent/Kill flags; fellow grant-holder → everything IT had accumulated
  (flags + kits, but never its own core identity — goop and swallow don't transfer); TOU-Mira/vanilla →
  the curated flag mapping (`CanVent` → Vent, `CanUseKillButton`/`UseVanillaKillButton` → Kill,
  `SwooperRole` → Swoop). `overwrite: true` (Kirby's non-accumulate lobby option) clears previous grants
  first. Both grant containers are plain mutable state set inside already-deterministic RPC/modifier
  handlers on every client (the Vulture-eat-count pattern) — no extra sync RPC.
- **Venting** syncs via `AbilityGrants.SyncVenting`: `Configuration.CanUseVent` reads the flag live, but
  MiraAPI bakes the vanilla `RoleBehaviour.CanVent` bool once at role setup, and the on-screen
  `ImpostorVentButton`'s visibility is only applied inside `HudManager.SetHudActive` (not per frame) —
  so it re-derives `CanVent` (base option OR flag, direction-agnostic so an overwrite that *drops* Vent
  also takes effect) and re-runs `SetHudActive` on the owner's client.
- **The pool** (`AbilityGrants.Pool`) is `PortableGrant[]` — entries carry flags, a kit type, or both;
  currently `[Sniper kit, Swoop flag, Daddy Hagrid kit]`. RPC-serialized by pool index (see the tier
  RPC above).

## Design decisions

- **Win condition: last one standing** (confirmed with the user), not a Vulture-style instant threshold
  win on goop count — Gooper eventually gains a permanent kill button, so an instant-win-on-count design
  sits oddly alongside actual combat power.
- **Adding to the pool costs one `PortableGrant` entry** — for any of this addon's roles it's just
  `new(GrantableAbility.None, typeof(XRole))` (the kit machinery does the rest); only TOU-Mira-sourced
  abilities need a recreated Layer-2 primitive first (like Swoop). The current pool is
  `[Sniper kit, Swoop, Daddy Hagrid kit]`. **Puppeteer's control is still deferred** — not because the
  framework can't hold it, but because the *ability itself* is a whole remote-control subsystem in
  TOU-Mira (per-client control-state dictionaries, a movement-hijacking Harmony patch, camera/light
  sync, disconnect handling). It is also a TOU-Mira role, so it would need the Layer-2 recreation path.
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
- Keybind collisions between two simultaneously-held borrowed kits (both on `SecondaryAction`) are
  possible; mouse clicks disambiguate. See the keybind allocation note above.
