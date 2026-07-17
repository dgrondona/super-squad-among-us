# Eraser

Impostor Support role. Use a secondary action button to mark a target player — by default, every marked player's modded role is stripped at the next meeting's exile screen and they become a plain vanilla Crewmate (even if they were an impostor or neutral). An `EraseImmediately` option can switch this to strip the role the instant the ability is used instead. Limited to a set number of uses (default 2) on top of a per-use cooldown escalation.

Files: `Roles/Impostor/EraserRole.cs`, `Buttons/Impostor/EraserEraseButton.cs`, `Modifiers/FutureErasedModifier.cs`, `Events/EraserEvents.cs`, `Options/Roles/Impostor/EraserOptions.cs`.

Design: ported from TheOtherRoles; see `docs/porting/tor-eraser-vulture.md`. The use limit and the immediate-erase option are both deliberate additions on top of TOR's original design (see "Design decisions" below).

## How it works

**Erase button.** `EraserEraseButton` is a secondary-action button that targets the nearest living player, limited to `MaxUses` (default 2; -1 is the infinite-uses sentinel, same convention as `ApparaterOptions.MaxUses`). Clicking behaves one of two ways depending on the `EraseImmediately` option:

- **Next meeting (default, `EraseImmediately` off).** The target is marked with `FutureErasedModifier` (synced via `RpcAddModifier`). No in-game notification — they'll only notice their role UI is gone at the next meeting.
- **Immediate (`EraseImmediately` on).** `EraserEvents.EraseRole` is called on the target right away, stripping their modded role on the spot.

**Mark state.** `FutureErasedModifier` is a synced modifier holding only the Eraser's reference. No in-game indicator on the victim; only the modifier's presence signals the mark. Once placed (non-immediate mode only), a mark persists until the exile screen.

**Exile-screen resolution.** `EraserEvents.EjectionEventHandler` runs on every client once the exile controller fires (deterministically, since synced modifiers arrived on all clients). Every still-marked player is resolved through the same `EraserEvents.EraseRole` helper the immediate-mode path uses.

**`EraserEvents.EraseRole` (shared strip logic).** For a given target: dead or disconnected players are skipped (deviation from TOR, documented inline; a role change on a dead player would replace their ghost role with a living one, breaking the dead spectator flow), otherwise the host sends `RpcChangeRole((ushort)RoleTypes.Crewmate)`. The eraser's own state is NOT checked — erases resolve even if the Eraser died in the meantime.

**Cumulative cooldown escalation.** Each successful mark adds 10 seconds to `CurrentCooldownAddition`, permanently. The cooldown is clamped to 5–240 seconds. The escalation persists across all meetings and only resets at game start (via `EraserEvents.RoundStartEventHandler`, which runs when the intro fires). The button singleton outlives games, so without this reset, cooldowns would carry over between rounds if the mod is left running.

**Target selection.** By default, only living crewmates can be targeted; the `CanEraseAnyone` option allows targeting impostors and neutrals too.

## Design decisions

- **Next meeting by default, immediate as an option.** TOR's original design only resolves at the exile screen; the user asked for an `EraseImmediately` toggle so hosts can opt into instant role-strips, defaulting off to match TOR's pacing.
- **Limited uses (default 2).** A deliberate addition on top of TOR, which had unlimited uses gated only by the escalating cooldown. Uses the same `-1`-is-infinite sentinel convention as Apparater's `MaxUses`, so a host can still set it back to unlimited.
- **Marks fire even if the Eraser dies.** TOR convention — the erase is committed when the mark is placed, not contingent on the Eraser surviving to the meeting.
- **Dead targets are skipped.** TOR would mark them in static bookkeeping, but here a role change on a dead player would swap their ghost role for a living one, breaking the spectator flow. Consistent with the mod's design.
- **Modifiers survive the erase.** Like Lovers, Mini, and other TOU-Mira modifiers — they are NOT cleared by the role strip. Only the modded role is removed; the modifier list is preserved.
- **No notification.** The target finds out when they see their role UI gone (or, in immediate mode, when their role disappears mid-round). User decision to avoid telegraphing the erase.
- **Permanent escalation.** Unlike a per-meeting cooldown penalty, the 10s escalation is never reset mid-game — the more you erase, the longer you wait, all game. Encourages strategic use.

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed — untestable solo (needs a second player). Highest-value checks: erase button appears and targets correctly, mark syncs to all clients, marked player's role strips at exile (becomes plain Crewmate), cooldown escalates per use, escalation persists across meetings, `EraseImmediately` strips the role on click instead of waiting for exile, and the button actually stops working after `MaxUses` erases.
- Role icon and ability sprite are placeholder art.
- Sound effects for mark and role strip are missing; TOR's equivalent sounds are extractable (see `docs/porting/README.md`).
- Verify edge case: Eraser is voted out and thus dies at the same exile screen where their marks resolve — marks should still fire (confirmed in code, but real-game verification needed).
- Verify that erasing a Lover results in a plain Crewmate Lover (modifier survives, role changes).
