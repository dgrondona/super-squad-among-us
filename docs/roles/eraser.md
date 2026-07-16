# Eraser

Impostor Support role. Use a secondary action button to mark a target player — at the next meeting's exile screen, every marked player's modded role is stripped and they become a plain vanilla Crewmate (even if they were an impostor or neutral). Each successful mark adds a permanent 10-second cooldown escalation, persisting across all meetings until the game ends.

Files: `Roles/Impostor/EraserRole.cs`, `Buttons/Impostor/EraserEraseButton.cs`, `Modifiers/FutureErasedModifier.cs`, `Events/EraserEvents.cs`, `Options/Roles/Impostor/EraserOptions.cs`.

Design: ported from TheOtherRoles; see `docs/porting/tor-eraser-vulture.md`.

## How it works

**Erase button.** `EraserEraseButton` is a secondary-action button that targets the nearest living player. Click to mark them with `FutureErasedModifier` (synced via `RpcAddModifier`). The target gets no in-game notification — they'll only notice their role UI is gone at the next meeting.

**Mark state.** `FutureErasedModifier` is a synced modifier holding only the Eraser's reference. No in-game indicator on the victim; only the modifier's presence signals the mark. Once placed, a mark persists until the exile screen.

**Exile-screen resolution.** `EraserEvents.EjectionEventHandler` runs on every client once the exile controller fires (deterministically, since synced modifiers arrived on all clients). For every marked player who is alive and not disconnected, the host sends `RpcChangeRole((ushort)RoleTypes.Crewmate)`, stripping their modded role. Dead targets are skipped (deviation from TOR, documented in EraserEvents.cs; a role change on a dead player would replace their ghost role with a living one, breaking the dead spectator flow). The eraser's own state is NOT checked — marks fire even if the Eraser died or was erased themselves.

**Cumulative cooldown escalation.** Each successful mark adds 10 seconds to `CurrentCooldownAddition`, permanently. The cooldown is clamped to 5–240 seconds. The escalation persists across all meetings and only resets at game start (via `EraserEvents.RoundStartEventHandler`, which runs when the intro fires). The button singleton outlives games, so without this reset, cooldowns would carry over between rounds if the mod is left running.

**Target selection.** By default, only living crewmates can be targeted; the `CanEraseAnyone` option allows targeting impostors and neutrals too.

## Design decisions

- **Marks fire even if the Eraser dies.** TOR convention — the erase is committed when the mark is placed, not contingent on the Eraser surviving to the meeting.
- **Dead targets are skipped.** TOR would mark them in static bookkeeping, but here a role change on a dead player would swap their ghost role for a living one, breaking the spectator flow. Consistent with the mod's design.
- **Modifiers survive the erase.** Like Lovers, Mini, and other TOU-Mira modifiers — they are NOT cleared by the role strip. Only the modded role is removed; the modifier list is preserved.
- **No notification.** The target finds out when they see their role UI gone. User decision to avoid telegraphing the erase mid-round.
- **Permanent escalation.** Unlike a per-meeting cooldown penalty, the 10s escalation is never reset mid-game — the more you erase, the longer you wait, all game. Encourages strategic use.

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed — untestable solo (needs a second player). Highest-value checks: erase button appears and targets correctly, mark syncs to all clients, marked player's role strips at exile (becomes plain Crewmate), cooldown escalates per use, and escalation persists across meetings.
- Role icon and ability sprite are placeholder art.
- Sound effects for mark and role strip are missing; TOR's equivalent sounds are extractable (see `docs/porting/README.md`).
- Verify edge case: Eraser is voted out and thus dies at the same exile screen where their marks resolve — marks should still fire (confirmed in code, but real-game verification needed).
- Verify that erasing a Lover results in a plain Crewmate Lover (modifier survives, role changes).
