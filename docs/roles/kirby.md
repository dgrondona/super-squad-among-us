# Kirby

Neutral Killing role. Swallow nearby players with the swallow ability — a near-literal copy of
the Pelican's devour (hidden, frozen, pinned to Kirby, digested with no body at the next meeting,
released alive at Kirby's position if Kirby dies first), per the user's own "swallow players like the
pelican" instruction. **The instant a player is swallowed**, Kirby inherits their portable abilities:
for any role from THIS addon that means the victim's **whole real button kit** (swallow an Apparater →
real Apparate, an RC-XD → the real car, a Sniper → the real snipe, etc. — automatic for every current
and future role here); for TOU-Mira/vanilla roles it means the curated flag primitives (Kill, Vent,
Swoop). See `docs/roles/gooper.md`'s "Shared ability-grant architecture" for the two-layer design this
builds on. **Accumulate vs. overwrite is a lobby option** (`Swallowed Abilities Accumulate`, default
on): off, each new swallow's grant replaces everything previous swallows granted — one copied power at
a time, like actual Kirby. Wins by outlasting the opposition, the same "last one standing" shape as
`GooperRole`/`SentinelRole`.

Kirby-specific files: `Roles/Neutral/KirbyRole.cs`, `Buttons/Neutral/KirbySwallowButton.cs`,
`Modifiers/KirbySwallowedModifier.cs`, `Events/KirbyEvents.cs`, `Options/Roles/Neutral/KirbyOptions.cs`.
Kirby has **no per-ability button classes of its own** — borrowed kits are the source roles' real
buttons (shown for Kirby via the grant-aware `Enabled` in `Buttons/SuperSquadRoleButton.cs`), and the
flag primitives are the shared `Granted*Button` singletons in `Buttons/GrantedAbilityButtons.cs`.

## How it works

**Swallow — a copy of `PelicanDevourButton`.** `KirbySwallowedModifier(PlayerControl kirby) : CarriedModifier(kirby)`
is a new **sealed sibling** alongside `DevouredModifier`/`CloakHiddenModifier`
(`Modifiers/CarriedModifier.cs`) — never subclassed from `DevouredModifier`, per that family's own
doctrine: event handlers iterate concrete modifier types, and subclassing one from another would make
each catch the other's carried players. `KirbySwallowButton.OnClick` is
`Target.RpcAddModifier<KirbySwallowedModifier>(PlayerControl.LocalPlayer)` (generic, no new RPC);
`GetTarget` excludes anyone already `HasModifier<CarriedModifier>()`.

**`KirbyEvents.cs` mirrors `PelicanEvents.cs`** for all three lifecycle cases: meeting-start digestion
(exile-style death, no body), Kirby-disconnect release, and Kirby-death mid-round release (everyone
alive at Kirby's death position). `KirbyRole.WinConditionMet()` carries the same
swallowed-but-undigested carve-out `PelicanRole.WinConditionMet()` uses — excluded from both the alive
count and the killers-alive count, so a full stomach (or a swallowed rival killer) can't soft-lock the
round.

**Ability inheritance happens at swallow-instant** (per the user's request), inside
`KirbySwallowedModifier.OnActivate` — which runs on every client because the modifier add is the synced
call, so `AbilityGrants.ApplyPortableGrant(kirbyRole, victimRole, overwrite)` stays deterministic
across clients with no new RPC (same reasoning as `VultureRole.EatenBodies++`; the accumulate option is
host-synced, so the overwrite branch is deterministic too). `ApplyPortableGrant` also re-syncs venting
in both directions — gaining Vent surfaces the vanilla vent button immediately, and an overwrite that
drops Vent removes it (see `AbilityGrants.SyncVenting`). A Kirby that dies before the next meeting
still releases everyone in its stomach alive — it just keeps whatever it already inherited (moot, since
a dead Kirby can't use abilities). This replaced an earlier digestion-time grant; the digestion handler
in `KirbyEvents.StartMeetingEventHandler` now only kills the swallowed players, no longer granting
anything.

## Design decisions

- **"Any abilities the players they swallowed had" is now literally true for every role in this addon**
  — a swallowed victim's role type goes into `KirbyRole.GrantedKits`, and the source role's real buttons
  show for Kirby (zero per-ability code; see the two-layer design in `docs/roles/gooper.md`). The two
  deliberate exceptions: a swallowed Gooper/Kirby hands over what it had *accumulated*, never its own
  core identity ability (goop/swallow don't transfer), and the Mafia promotion-entangled roles
  (Godfather/Mafioso) grant only their generic flags. **TOU-Mira roles remain curated flags** (Kill,
  Vent, Swoop) because their buttons cannot be borrowed — their ability RPCs reject non-holder senders
  on every client (audited: all 49 of them) and their `Role`-typed buttons NRE for a borrower; each
  TOU-Mira ability worth inheriting gets recreated once as a Layer-2 primitive, on demand.
- **Win condition: last one standing**, matching Gooper's confirmed decision — not separately asked, but
  the same archetype (a Neutral Killing role that eventually accumulates real killing power).
- **`CanUseVent` combines the base option with the inherited flag**: `KirbyOptions.CanVent || UnlockedAbilities.HasFlag(Vent)` —
  Kirby can vent if either the base toggle is on or it digested a venting-capable victim.

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed — untestable solo (needs players to swallow, ideally ones holding
  different abilities). Highest-value checks: swallowing hides/freezes exactly like the Pelican; the
  inherited kit appears *immediately on swallow* (swallow a Sniper → the real Snipe button at once;
  swallow an Apparater → the map-teleport button); the accumulate-off option makes a second swallow
  replace the first grant (including the vent button disappearing if Vent is lost); dying before the
  meeting releases the swallowed player alive; meeting digestion kills swallowed players with no body.
  Keybinds: granted Kill → Primary, Swallow → **Tertiary**, Swoop → Modifier, Vest → click-only;
  borrowed kit buttons keep their source keybinds (mostly Secondary — see the collision note in
  `docs/roles/gooper.md`).
- Role icon and every ability button sprite are placeholder art
  (`SuperSquadAssets.NeutralPlaceholderIcon`/`NeutralPlaceholderButton`).
- Vanilla kill capability is now detected via `RoleBehaviour.CanUseKillButton` (in addition to
  `ICustomRole.Configuration.UseVanillaKillButton`), so digesting a vanilla Impostor grants Kill —
  unverified in-game. TOU-Mira roles with bespoke kill buttons (Sheriff, Juggernaut, etc.) still grant
  nothing; that's the TOU-Mira limitation above, not a bug.
- Passive, non-button role behaviors (e.g. Invisible Boy's passive invisibility, vision modifiers) do
  NOT transfer with a kit — kits transfer buttons. Accepted v1 scope.
- Puppeteer's control ability is excluded from Kirby's inheritable set for the same reason it's deferred
  from Gooper's pool (see `docs/roles/gooper.md`) — a whole remote-control subsystem on a TOU-Mira
  role, not yet separately scoped.
