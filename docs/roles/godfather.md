# Godfather

Impostor Killing role and leader of the mafia trio. Uses the vanilla kill and sabotage buttons. The Mafioso cannot kill or sabotage while the Godfather is alive; upon the Godfather's death, the Mafioso's abilities unlock immediately (cooldown not reset). The mafia trio spawns together via a single spawn-chance roll — they cannot spawn any other way.

Files: `Roles/Impostor/GodfatherRole.cs`, `Patches/MafiaAssignmentPatch.cs`, `Patches/MafiaLabelsPatch.cs`, `Options/Roles/Impostor/MafiaOptions.cs`.

Design: ported from TheOtherRoles; see `docs/porting/tor-mafia.md`.

## How it works

**Spawn trigger.** `MafiaAssignmentPatch` runs host-side with very low priority after TOU-Mira's role selection (once per game, deterministically). It rolls a single spawn-chance check (configurable 0–100%, default 50%); if it passes and there are 3+ impostors alive, three already-assigned impostors are converted to Godfather, Mafioso, and Mafia Janitor. The three impostors with the lowest role precedence are selected first — vanilla impostors before custom roles, then random order — ensuring configured custom roles are preserved when possible.

**Mafioso gating.** The Godfather is a plain impostor with vanilla kill and sabotage. The Mafioso's kill and sabotage are gated by `MafiosoGatePatches` (local-only Harmony prefixes on `KillButton.DoClick` and `SabotageButton.DoClick`), which block these actions while any living Godfather exists. When the Godfather dies, the Mafioso's buttons reappear immediately via a per-frame HUD state check (mirrors TOR behavior; vanilla only re-shows buttons on hud refreshes, which don't occur mid-round).

**Mafia labels.** Every frame, `MafiaLabelsPatch` appends "(G)" to the Godfather's name, visible only to other mafia members. Dead impostors lose the tag — a deviation from TOR where dead mafia stay labeled.

**Hidden settings.** The Godfather, Mafioso, and Mafia Janitor all have `DefaultRoleCount = 0` and `DefaultChance = 0`, with settings hidden in the options menu. They spawn only via the assignment patch.

## Design decisions

- **Single spawn roll for the whole trio.** One RNG call per game determines whether *any* mafia spawns; either all three are assigned or none. TOR-faithful.
- **Spawns on existing impostors.** The patch converts already-assigned impostors rather than claiming extra seats, ensuring the mafia doesn't arbitrarily increase the impostor count.
- **Mafioso unlock is immediate, cooldown not reset.** TOR behavior — when the Godfather dies mid-round, the Mafioso's kill button reappears in the same frame with its existing cooldown intact. Prevents a power spike.
- **No intro reveal.** The mafia members find each other via the per-frame name tags — no special intro screen like some TOU roles.

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed — untestable solo (needs 3+ impostors at game start). Highest-value checks: trio assignment on start, Mafioso button gating/ungating when Godfather dies, and mafia name tags visible only to team members.
- Role icon and ability sprites are placeholder art; Godfather and Mafioso currently share ATR's generic impostor icon.
- Mafioso unlock edge cases should be confirmed (e.g., Godfather killed by a Sheriff mid-round while Mafioso is using their ability).
- The spawn-chance roll should be verified against the configured option in actual gameplay.
