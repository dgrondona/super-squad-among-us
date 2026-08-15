# Sentinel

Neutral Killing role. Two abilities: a single-target Kill and an area-of-effect Explode that damages
everyone in range. Wins by outlasting the opposition, the same "last one standing" shape as
`GooperRole`/`KirbyRole`.

Files: `Roles/Neutral/SentinelRole.cs`, `Buttons/Neutral/SentinelExplodeButton.cs`,
`Buttons/Neutral/SentinelKillButton.cs`, `Modules/Explode.cs`.

No design-history doc predates this file — Sentinel was built by copying `GlitchRole` (win-condition
scaffolding) and TOU-Mira's `ArsonistIgniteButton`/`Ignite` (the explode ability) rather than written
fresh; see the follow-ups below for what that copy left unfinished.

## How it works

Kill and Explode each put the other on full cooldown when used (`ResetCooldownAndOrEffect()` on the
sibling button), so the two abilities can't be chained for a double-kill. The win condition follows
`GlitchRole`'s generic Neutral Killing formula, gated by `KillersAliveCount` — agnostic to which of the
two buttons did the killing.

## Not yet verified in-game / known follow-ups

- **Unrenamed copy-paste residue from the roles this was built from (confirmed, no functional bug).**
  `WinConditionMet()` is a byte-for-byte copy of `GlitchRole.WinConditionMet()`, including the leftover
  local variable name `glitchCount`. The explode ability is a close adaptation of TOU-Mira's
  `ArsonistIgniteButton`/`Ignite`, but internal identifiers were never renamed either: a local variable
  is still `dousedPlayers` even though Sentinel has no douse/mark step (it explodes everyone in range
  outright), `Explode.CreateExplode` still uses `igniteRadius`/a local named `ignite`, and the explosion
  plays Arsonist's ignite sound rather than a Sentinel-specific cue. None of this is a behavioral bug —
  worth a rename/asset pass before this role is considered finished, not just functional.
- Role icon and both ability button sprites are placeholder art.
- Manual in-game verification needed — untestable solo (needs a second player). Highest-value checks:
  Explode's radius and damage feel correct, Kill/Explode correctly gate each other's cooldown, and the
  win condition triggers at the right threshold.
