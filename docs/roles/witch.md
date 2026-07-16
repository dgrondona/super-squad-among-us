# Witch

Impostor Support role. Channel a hex on the nearest target (configurable to any player on the map). If the channel completes, the target is marked for death and dies at the next meeting ejection — unless the Witch was voted out and the "save on vote" option is enabled. Each successful hex adds cumulative extra cooldown to future hexes (reset each meeting), encouraging strategic use.

Files: `Roles/Impostor/WitchRole.cs`, `Buttons/Impostor/WitchHexButton.cs`, `Modifiers/HexedModifier.cs`, `Events/WitchEvents.cs`, `Patches/WitchMeetingPatch.cs`, `Options/Roles/Impostor/WitchOptions.cs`.

Design: ported from TheOtherRoles; see `docs/porting/README.md`.

## How it works

**Channel-cast pattern.** Click to start a 0–10s channel on the closest valid target; the channel auto-cancels if the target changes (they move away or someone closer walks in). No re-click during the channel; it either completes or self-cancels.

**Hex state is a modifier.** `HexedModifier` (synced via `RpcAddModifier`) holds only the Witch's reference — no in-game indicator on the victim. The victim gets no feedback; the meeting hex-overlay (`WitchMeetingPatch`) is the only tell.

**Ejection-time resolution.** `WitchEvents.EjectionEventHandler` runs on every client once the exile controller fires (deterministically, since hex state arrived via synced modifiers): every hexed player dies exile-style (no body) UNLESS the Witch was voted out and `VotingWitchSavesTargets` is on.

**Cumulative cooldown.** Each successful cast adds `AdditionalCooldown` to `CurrentCooldownAddition`, which persists across multiple hexes in one round. Reset to 0 when a meeting starts (TOU-Mira convention). Optional vanilla kill-button sync: if `TriggerBothCooldowns` is on, a successful hex also puts the vanilla kill cooldown on cool-down (impostors only).

**Meeting hex-overlay.** `WitchMeetingPatch` shows the hex sprite next to hexed players' names in meetings, visible to everyone. Runs every MeetingHud update so hexes cast right before the meeting and mid-meeting deaths stay accurate.

## Design decisions

- **Pure hex state.** Victim is alive but marked to die at ejection — no in-game indication, no death message until ejection completes.
- **Asymmetric save rule.** Only a voted-out Witch saves targets. A Witch killed mid-meeting does NOT save — only the exile vote matters. (TOR convention, deliberate asymmetry.)
- **Channel cancels if target changes.** Prevents fire-and-forget casting while moving around. Forces the Witch to commit and stay focused on a target.
- **Cumulative cooldown.** Not reset per cast, only per meeting. Encourages tactical use mid-round — the more you hex, the longer you wait for the next one.
- **Hex overlay visible to everyone.** Including the Witch and victim after the hex is cast. Transparent in hindsight.

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed.
- Role icon and ability sprite are placeholder art.
- Cast start and completion sounds are missing. TOR's `witchSpell` sound is extractable; see `docs/porting/README.md`.
- Cumulative-cooldown reset timing on meeting start mirrors TOR but is unverified in practice.
- Edge case: if the Witch dies mid-cast (target changes or kills the Witch), the channel stops and the cast fails (no hex placed). This is TOR behavior but should be confirmed.
