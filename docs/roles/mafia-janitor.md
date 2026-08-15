# Mafia Janitor

Impostor Support role and member of the mafia trio. Has no kill button — instead, a secondary action button cleans one dead body per cooldown, instantly removing it for all players. Distinct from TOU-Mira's standalone Janitor role. The mafia trio spawns together via a single spawn-chance roll — they cannot spawn any other way.

Files: `Roles/Impostor/MafiaJanitorRole.cs`, `Buttons/Impostor/MafiaJanitorCleanButton.cs`, `Modules/SuperSquadBodies.cs`, `Patches/MafiaLabelsPatch.cs`, `Options/Roles/Impostor/MafiaOptions.cs`.

Design: ported from TheOtherRoles; see `docs/porting/tor-mafia.md`.

## How it works

**Clean button.** `MafiaJanitorCleanButton` is a secondary-action button (same as most TOU support roles' abilities). It targets the nearest dead body within range and instantly removes it via `SuperSquadBodies.RpcMafiaCleanBody`, which destroys the body GameObject on all clients. The button is always available at the configured cooldown (default 30s, configurable 10–60s + map bonuses).

**Instant removal.** Unlike TOU-Mira's Janitor, which has a configurable clean delay and use limit per meeting, the Mafia Janitor cleans instantly with no delay or limit. Bodies are gone immediately, leaving no corpse report opportunity.

**Pet removal.** `SuperSquadBodies.DestroyBodies` mirrors TOU-Mira's own Janitor/Chef clean behavior: if the host's Vanilla Tweaks options have "Remove Pets Upon Janitor/Chef Clean" on and pet visibility set to Always Visible, the cleaned player's pet is removed along with the body (see `docs/il2cpp-gotchas.md` for why this isn't just a direct call into TOU-Mira's own method).

**Crime scene clearing.** `DestroyBodies` also calls `CrimeSceneComponent.ClearCrimeScene` on the body before destroying it, same as TOU-Mira's own `JanitorRole.RpcCleanBody` — otherwise a cleaned body's crime scene would stay behind for the Forensic role to inspect even after the body is gone.

**No kill button.** Configuration sets `UseVanillaKillButton = false`, so the Mafia Janitor relies entirely on the clean button. They are a support role, not a killer.

**Mafia labels.** Every frame, `MafiaLabelsPatch` appends "(J)" to the Mafia Janitor's name, visible only to other mafia members. Dead impostors lose the tag — a deviation from TOR where dead mafia stay labeled.

**Hidden settings.** The Mafia Janitor has `DefaultRoleCount = 0` and `DefaultChance = 0`, with settings hidden. They spawn only via the mafia assignment patch.

## Design decisions

- **No delay, no limit.** TOR's behavior — contrast with TOU-Mira's more restrictive standalone Janitor. Clean cooldown is the only throttle.
- **Instant removal.** Bodies vanish immediately, preventing forensic plays like corner reports or body-stacking.
- **Support role.** No kill button by design — the mafia janitor cleans, not kills. Imposes team coordination: the Godfather and Mafioso are the killers.
- **Shared spawn roll.** Like the Godfather and Mafioso, the Janitor spawns only as part of the mafia trio assignment, not independently.

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed — untestable solo (needs 3+ impostors at game start). Highest-value checks: clean button appears, targets the nearest body, removes it instantly for all players, cooldown behaves as configured, and Janitor name tag visible only to team members.
- Role icon and ability sprite are placeholder art.
- Body cleanup should be verified in a real lobby with multiple bodies and a second player to confirm visibility changes.
- Verify that TOU Janitor-cleaned bodies (from a standalone Janitor, if present) don't interfere with the Mafia Janitor's clean mechanics.
- Mafia Janitor allows sabotage (`CanUseSabotage = true` by inheritance), unlike TOR, which blocks this
  role from sabotage entirely — undecided whether that's intentional; flagging so it's a deliberate call,
  not an oversight. See `docs/roles/mafioso.md`.
