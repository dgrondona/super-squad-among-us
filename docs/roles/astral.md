# Astral

Impostor Support role. Click the secondary ability button to enter ghost form: become invisible, walk through walls, and escape dangerous situations. After the duration expires, snap back to the phased spot and linger invisible for a few seconds before fully returning. The form cannot be cancelled early once activated.

Files: `Roles/Impostor/AstralRole.cs`, `Buttons/Impostor/AstralFormButton.cs`, `Modifiers/AstralFormModifier.cs`, `Modifiers/AstralLingerModifier.cs`, `Modifiers/TimedInvisibilityModifier.cs`, `Options/Roles/Impostor/AstralOptions.cs`.

Design: adapted from AllTheRoles per the user's own spec; see `docs/porting/README.md`.

## How it works

**Two-phase modifier handoff.** `AstralFormModifier` (ghost phase) and `AstralLingerModifier` (grace phase) both inherit from `TimedInvisibilityModifier`, which handles shared invisibility rendering. Ghost phase disables the player's collider on activation, then re-enables it and snaps the player back via `RpcSnapTo` on deactivation (client-authoritative). If configured, the linger phase triggers automatically after snap-back; both modifiers auto-expire on timer and remove themselves. The button's `EffectDuration` equals form + linger time, so the cooldown doesn't start until both phases end.

**Shared invisibility rendering.** Both phases use `TimedInvisibilityModifier`, which implements Swooper's viewer rule: fellow impostors and the informed dead see a faint outline (0.1α black), everyone else sees nothing. Visibility state is re-asserted every tick to survive vent/ladder animations (vanilla flips `Player.Visible` back on during those).

**Syncing.** Modifiers sync via `RpcAddModifier` (MiraAPI standard); movement uses `RpcSnapTo` (TOU-Mira standard). No custom RPCs.

## Design decisions

- **Two-phase structure** isolates ghost mechanics (collider disable, wall-pass) from linger visibility (collision re-enabled but invisible) — clean separation of concerns and clearer flow.
- **Cannot cancel early.** Once phased, the tether runs its course. Prevents abuse of the movement immunity.
- **Linger phase deliberate.** Returning is a vulnerable moment; linger gives the Astral cover for that split second, making it less obvious you just phased.
- **No custom RPCs.** Movement and state arrive via standard TOU-Mira channels (`RpcSnapTo`, `RpcAddModifier/RemoveModifier`), keeping the implementation simple.

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed (no automated test suite).
- Role icon and ability sprite are placeholder art — swap out when real art exists.
- Kill-button usability while phased needs verification: can the impostors' standard kill button be used during ghost form, or is it disabled?
- `TimedInvisibilityModifier.ApplyLocalVisibility()` re-asserts visibility every tick to counter vanilla flipping it back during vent/ladder animations; this mirrors InvisibleBoyModifier's fix but the underlying vanilla bug (if still present) should be re-confirmed.
