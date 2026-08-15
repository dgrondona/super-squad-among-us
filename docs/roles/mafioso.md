# Mafioso

Impostor Killing role and member of the mafia trio. Uses the vanilla kill and sabotage buttons, but both are gated by a local-only Harmony patch while any living Godfather exists. Upon the Godfather's death, both abilities unlock immediately — the kill cooldown is NOT reset (TOR behavior). The mafia trio spawns together via a single spawn-chance roll — they cannot spawn any other way.

Files: `Roles/Impostor/MafiosoRole.cs`, `Patches/MafiosoGatePatches.cs`, `Patches/MafiaLabelsPatch.cs`, `Options/Roles/Impostor/MafiaOptions.cs`.

Design: ported from TheOtherRoles; see `docs/porting/tor-mafia.md`.

## How it works

**Gated kill and sabotage.** `MafiosoGatePatches` contains two Harmony prefixes on `KillButton.DoClick` and `SabotageButton.DoClick`, mirroring TOR's `UpdatePatch.cs` and `UsablesPatch.cs`. Both methods return false (block the action) while the local Mafioso is alive and any living Godfather exists. The gates are purely local UI logic — no RPCs, no server-side validation.

**Button visibility on gate lift.** Vanilla only re-shows the kill/sabotage buttons on full HUD refreshes (e.g., after a meeting or report). The Godfather can die mid-round (e.g., killed by a Sheriff) without triggering a refresh. `MafiosoGatePatches.HudUpdatePostfix` runs every frame to immediately hide both buttons while gated and re-show them when the gate lifts (once, on the transition from gated to ungated), preserving the vanilla button lifecycle.

**Cooldown preserved on unlock.** When the Godfather dies, the Mafioso's kill button reappears with its existing cooldown — no reset. This matches TOR behavior and prevents a power spike where an imprisoned Mafioso suddenly has a fresh kill.

**Mafia labels.** Every frame, `MafiaLabelsPatch` appends "(M)" to the Mafioso's name, visible only to other mafia members. Dead impostors lose the tag — a deviation from TOR where dead mafia stay labeled.

## Design decisions

- **Gates are local-only.** No RPC, no server validation. Both impostors and non-impostors see a Mafioso unable to click kill/sabotage while gated; the gate logic itself runs only on the local client.
- **Button lifecycle mirrors vanilla.** The button stays on cooldown and in the same state, just hidden. When the gate lifts, it re-appears with no cooldown reset.
- **Asymmetric kill gating.** Only the Mafioso is gated; the Godfather, Mafia Janitor, and all other roles are unaffected. The Godfather's death is the signal that the mafia hierarchy has changed.

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed — untestable solo (needs 3+ impostors at game start). Highest-value checks: Mafioso button hidden while Godfather is alive, immediately re-appear when Godfather dies, cooldown preserved on gate lift, and Mafioso name tag visible only to team members.
- Role icon and ability sprite are placeholder art; Mafioso currently shares ATR's generic impostor icon with the Godfather.
- Edge case: Mafioso kills someone while the Godfather is alive but dies before the kill resolves — should be confirmed.
- Gating behavior during meetings and vents should be spot-checked (e.g., can't sabotage during a meeting anyway, gate is redundant then).
- Separately, Mafia Janitor allows sabotage at all (`CanUseSabotage = true`), where TOR blocks that role
  from sabotage entirely — undecided whether that's an intentional divergence; see
  `docs/roles/mafia-janitor.md`.
