# Elusive

Crewmate Protective role. Shield ability on the secondary action (Veteran-alert shape: duration +
cooldown + limited uses). While the shield is up, anyone who interacts with the Elusive — a kill
attempt or a targeted ability button — has the interaction cancelled and is teleported to a random
reachable spot on the map.

Files: `Roles/Crewmate/ElusiveRole.cs`, `Buttons/Crewmate/ElusiveShieldButton.cs`,
`Modifiers/ElusiveShieldModifier.cs`, `Events/ElusiveEvents.cs`,
`Options/Roles/Crewmate/ElusiveOptions.cs`, plus
`Modules/WalkableRegionSolver.TryFindRandomReachablePoint`.

Design: interception pattern copied from TOU-Mira's Veteran (`VeteranEvents.CheckForVeteranAlert`),
with the retaliation kill swapped for a teleport.

## How it works

**Interception (Veteran's exact two hooks).** `ElusiveEvents` registers at priority 1 on:

- `MiraButtonClickEvent` — local-only; fires from MiraAPI's PassiveButton wrapper *before*
  `ClickHandler`, so cancelling means the attacker's button consumes **no cooldown and no use**.
  Catches any `CustomActionButton<PlayerControl>` aimed at the shielded Elusive (kill buttons,
  devour, slide tackle, hide, ...).
- `BeforeMurderEvent` — runs on every client; the cancel is deterministic because shield state
  arrived via synced modifiers (house rule: no host gating, see `docs/il2cpp-gotchas.md`).

Only the attacker's own client performs the teleport (`source.AmOwner` gate, `RpcSnapTo` is
client-authoritative) — the same split Veteran uses for its retaliation kill and the Pelican for its
release snap.

**Veteran parity choices.** Indirect attackers (`IndirectAttackerModifier`, e.g. an Arsonist's
douse) are blocked but not teleported; shield-piercing ones (`IgnoreShield`) pass through entirely.
Veteran's `InvulnerabilityModifier` carve-out was deliberately dropped — it exists to avoid
retaliation-murdering an unkillable Pestilence (softlock), and teleporting one is harmless.

**Random destination.** `WalkableRegionSolver.TryFindRandomReachablePoint(origin, probeRadius,
minDistance)` aims the existing grid search at up to 3 random raw targets (random spawn center +
`insideUnitCircle * 25`), preferring a result ≥ 5 units from the attacker and falling back to the
farthest candidate. Reachability is guaranteed by the existing solver (including its spawn-anchor
second seed for cross-map jumps). Pure `UnityEngine.Random` is fine — exactly one client computes,
the snap syncs. If everything fails (no ShipStatus, no candidates), the interaction is still
cancelled; the attacker just stays put.

**No shield visual.** Deliberate: a Medic-style overlay would tell attackers not to bother,
defeating the bluff. The Elusive sees the button's effect fill and the "Shielded" modifier timer.

## Design decisions

- **Shield ends at meetings and on death** (modifier removes itself), matching Veteran alert.
- **Max Shields is the button's uses counter only** — no role-side counter; Veteran's `Alerts` field
  exists for task-refund plumbing this role doesn't adopt.
- **Worst-case cost accepted:** up to 3 × ≤8000-cell greedy searches in one frame on the attacker's
  client for a one-shot deflect; typical case terminates on the first attempt.

## Not yet verified in-game / known follow-ups

- Needs 2-client verification: shielded kill attempt → attacker teleported and Elusive alive,
  cancelled targeted ability consuming no cooldown, destination variety/reachability on Skeld,
  Polus, and Airship (tune `RandomTargetMaxRadius`/`MinTeleportDistance` if spots cluster), shield
  expiry restoring normal kills.
- Known Veteran-parity race: shield-expiry timer skew between clients can produce edge-of-window
  disagreement on whether an attack landed. Accepted.
- No dedicated art — role icon uses the generic Crewmate placeholder
  (`Resources/Placeholders/Crewmate.png`); the Shield button uses the generic HUD button placeholder
  (`Resources/Placeholders/GenericButton.png`).
