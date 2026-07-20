# Sui

Crewmate Protective role. Protect one living player at a time with the primary ability button
(re-protecting someone new drops the previous protection first). If anyone uses an ability on — or
attempts to kill — the protected player, Sui gains a one-shot retaliation kill locked onto whoever did
it, with an arrow pointing them out. Three options adjust the scope: retaliate against anyone rather
than only the actual interactor; die if the wrong person is retaliated against; and narrow the trigger
to only an impostor's kill attempts. The retaliation window lasts until it's used or the next meeting
starts.

Files: `Roles/Crewmate/SuiRole.cs`, `Buttons/Crewmate/SuiProtectButton.cs`,
`Buttons/Crewmate/SuiRetaliateButton.cs`, `Modifiers/SuiProtectedModifier.cs`, `Events/SuiEvents.cs`,
`Options/Roles/Crewmate/SuiOptions.cs`.

Design: the protect button copies `MedicShieldButton`'s single-target shape; the interception is a
literal template copy of this repo's own `Events/ElusiveEvents.cs` (Veteran's interception pattern),
detecting rather than cancelling; the wrong-target self-destruct option copies Sheriff's misfire
pattern (`SheriffShootButton.Misfire`).

## How it works

**Protected state is a synced modifier on the target, not a private field on `SuiRole`** — this falls
directly out of how the interception has to work: `MiraButtonClickEvent` fires on the *attacker's own
client*, which can't see a private field on a third party's role instance, only synced modifier state on
the click's target. `SuiProtectedModifier(PlayerControl sui) : BaseModifier` is a pure marker
(`HexedModifier`'s shape — just a `Sui` back-reference, `OnMeetingStart`/`OnDeath` both remove it), set
via `Target.RpcAddModifier<SuiProtectedModifier>(PlayerControl.LocalPlayer)` (generic, no new RPC).
`SuiRole.Protected` is kept as a same-client convenience mirror, purely so
`SuiProtectButton.OnClick` knows whether to drop a previous protection first (matching `MedicRole.Shielded`'s role).

**Interception — `SuiEvents`, copied from `ElusiveEvents` but detect-don't-cancel.** Same two hooks
(`MiraButtonClickEvent` local-only before `ClickHandler`, `BeforeMurderEvent` on every client since
protection state is a synced modifier), but no `.Cancel()` call — the interaction proceeds normally.
When `OnlyImpostorKillsTrigger` is on, the click-event handler bails immediately (skipping the broad
"any ability button" catch-all entirely) so only actual kill attempts, further filtered to
impostor-aligned sources, can arm the retaliation.

**Retaliation button** (`SuiRetaliateButton`) is hidden/disabled (`Enabled` override) until armed via
`Arm(interactor)`, called from `SuiEvents` on the Sui's own client only. `GetTarget()` is hard-locked to
the armed interactor unless `CanTargetAnyone` is on, in which case it falls back to
`GetClosestLivingPlayer`. An arrow (`MiscUtils.CreateArrow`, synced every `FixedUpdate` like Vulture's
body arrows) always points at the locked interactor regardless of that option — it's a hint about who
triggered the window, not a restriction on who can be killed.

**Wrong-target self-destruct** (`SuiOptions.DiesOnWrongTarget`) only fires when `CanTargetAnyone` is
also on: `OnClick` compares the actual `Target` against the locked interactor, and if they differ (and
both options are on), calls a second `RpcCustomMurder(PlayerControl.LocalPlayer)` on Sui themself — the
exact pattern Sheriff's `Misfire()` uses for a wrong guess.

**No new RPCs.** Protect/re-target uses the generic `RpcAddModifier`/`RpcRemoveModifier<SuiProtectedModifier>`;
the retaliation kill uses the existing generic `RpcCustomMurder`.

## Design decisions

- **Retaliation window persists until used or the next meeting starts** (confirmed with the user) — no
  separate countdown timer, matching how every other timed/armed state in this codebase self-clears at
  a meeting.
- **`DiesOnWrongTarget` is only meaningful when `CanTargetAnyone` is on** — with it off, the target is
  hard-locked to the actual interactor, so there's no "wrong person" to pick. The option's locale
  description should make this dependency clear rather than leaving it silently inert when
  `CanTargetAnyone` is off.
- **The retaliation kill is a normal `RpcCustomMurder` call**, so it's naturally still subject to any
  other shield/protect modifier the interactor happens to be carrying — no special-case bypass.
- **Sui can retarget protection freely** (cooldown-gated, like any other ability use), rather than a
  Medic-style limited number of retargets — simpler, and nothing in the brief asked for a retarget cap.

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed — untestable solo (needs a second/third player to interact with
  the protected target). Highest-value checks: protecting someone and having another role use an
  ability on them (or attempt a kill) arms a retaliation locked to that interactor by default; each of
  the three options toggled independently behaves as described above; the window survives until a
  meeting starts if unused.
- Role icon and both button sprites have no dedicated art yet — both use
  `SuperSquadAssets.CrewmatePlaceholderIcon`/`CrewmatePlaceholderButton`.
- Multiple simultaneous Suis (if `MaxRoleCount` allows more than one) aren't specially handled — each
  tracks its own `Protected`/retaliation state independently, with no shared coordination. Not expected
  to cause bugs, but untested.
