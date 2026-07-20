# Kirby

Neutral Killing role. Swallow nearby players with the primary ability button — a near-literal copy of
the Pelican's devour (hidden, frozen, pinned to Kirby, digested with no body at the next meeting,
released alive at Kirby's position if Kirby dies first), per the user's own "swallow players like the
pelican" instruction. **The instant a player is swallowed**, Kirby permanently inherits a curated set of
their portable abilities (kill, vent, snipe, swoop) — see `docs/roles/gooper.md`'s "Shared ability-grant
architecture" section, which this role builds on rather than duplicating. Wins by outlasting the
opposition, the same "last one standing" shape as `GooperRole`/`SentinelRole`.

Files: `Roles/Neutral/KirbyRole.cs`, `Buttons/Neutral/KirbySwallowButton.cs`,
`Buttons/Neutral/KirbyKillButton.cs`, `Buttons/Neutral/KirbySnipeButton.cs`,
`Buttons/Neutral/KirbySwoopButton.cs`, `Modifiers/KirbySwallowedModifier.cs`,
`Modifiers/KirbySwoopModifier.cs`, `Events/KirbyEvents.cs`, `Options/Roles/Neutral/KirbyOptions.cs`.
Shared foundation: `Modules/AbilityGrants.cs`, `Buttons/GrantedAbilityButtons.cs`,
`Modifiers/GrantedSwoopModifierBase.cs` (see `docs/roles/gooper.md` for the full design).

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
call, so `kirbyRole.UnlockedAbilities |= AbilityGrants.GetPortableAbilities(Player.Data.Role)` stays
deterministic across clients with no new RPC (same reasoning as `VultureRole.EatenBodies++`). If the
inherited set includes `Vent`, it also sets the vanilla `kirbyRole.CanVent` bool (see the vent note in
`docs/roles/gooper.md`). A Kirby that dies before the next meeting still releases everyone in its stomach
alive — it just keeps whatever it already inherited (moot, since a dead Kirby can't use abilities). This
replaced an earlier digestion-time grant; the digestion handler in `KirbyEvents.StartMeetingEventHandler`
now only kills the swallowed players, no longer granting anything.

## Design decisions

- **The curated portable-ability set is exactly Gooper's vocabulary: Kill, Vent, Snipe, Swoop.** Per the
  user's own framing, "any abilities the players they swallowed had" taken 100% literally would mean
  cloning behavior from every existing and future role in the whole roster — there's no lighter-weight
  primitive for that than MiraAPI/TOU-Mira's full role-swap (`ChangeRole`), which tears down and
  reinstantiates the entire role rather than copying specific behavior. A curated, extensible table
  (`AbilityGrants.GetPortableAbilities`) is the only approach consistent with what actually exists to
  build on — see `docs/roles/gooper.md` for the table itself and how to grow it.
- **Win condition: last one standing**, matching Gooper's confirmed decision — not separately asked, but
  the same archetype (a Neutral Killing role that eventually accumulates real killing power).
- **`CanUseVent` combines the base option with the inherited flag**: `KirbyOptions.CanVent || UnlockedAbilities.HasFlag(Vent)` —
  Kirby can vent if either the base toggle is on or it digested a venting-capable victim.

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed — untestable solo (needs players to swallow, ideally ones holding
  different abilities). Highest-value checks: swallowing hides/freezes exactly like the Pelican; Kirby
  gains the inherited ability *immediately on swallow* (e.g. swallow a Sniper → Snipe button appears at
  once); dying before the meeting releases the swallowed player alive; meeting digestion kills swallowed
  players with no body. Note the inherited ability keybinds (Kill → Secondary, Snipe → Tertiary, Swoop →
  Modifier) so they don't collide with Swallow on Primary.
- Role icon and every ability button sprite are placeholder art
  (`SuperSquadAssets.NeutralPlaceholderIcon`/`NeutralPlaceholderButton`).
- Same accepted gap as Gooper: `AbilityGrants.GetPortableAbilities`'s kill-capability check only covers
  this addon's own `ICustomRole`s. Digesting a vanilla base-game role (a plain Sheriff, Impostor, etc.)
  won't currently grant `Kill`, even though that victim could actually kill. Worth revisiting if Kirby
  playtests feel weaker than intended against vanilla roles.
- Puppeteer's control ability is excluded from Kirby's inheritable set for the same reason it's deferred
  from Gooper's pool (see `docs/roles/gooper.md`) — a whole remote-control subsystem, not yet
  separately scoped.
