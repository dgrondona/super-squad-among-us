# Pelican

Neutral Killing role. Devour nearby players with the primary ability button. Devoured players are hidden from the round and pinned to follow the Pelican's position (their camera spectates the Pelican). At the start of the next meeting, all devoured players die with no bodies. If the Pelican is killed mid-round, everyone in its stomach is released alive at the spot it died. Devoured players don't count as alive for the Pelican's win condition — preventing a soft-lock from a full stomach.

Files: `Roles/Neutral/PelicanRole.cs`, `Buttons/Neutral/PelicanDevourButton.cs`, `Modifiers/DevouredModifier.cs`, `Events/PelicanEvents.cs`, `Options/Roles/Neutral/PelicanOptions.cs`.

Design: adapted from AllTheRoles per the user's own spec; see `docs/porting/README.md`.

## How it works

**Devour button.** Targeted ability on primary action, range-based like a vanilla kill button. Target any player, impostors included (neutral killer).

**Devoured modifier.** `DevouredModifier` (synced via `RpcAddModifier`) hides the player and freezes movement. While devoured, the player is pinned to the Pelican's position every `FixedUpdate` — their camera follows the Pelican. Movement and report buttons are disabled for the owner. No in-game indicator except to the devoured player and the informed dead (faint outline).

**Meeting digestion.** `PelicanEvents.StartMeetingEventHandler` runs before vote areas are built: every devoured player dies via `Exiled()` (exile-style, no body). Runs deterministically on every client since devoured state arrived via synced modifiers.

**Mid-round release.** `PelicanEvents.PlayerDeathEventHandler` runs when the Pelican dies: every devoured player is released with a position snap via `RpcSnapTo` (client-authoritative). Devoured player's own client performs the snap.

**Win condition excludes devoured.** `PelicanRole.WinConditionMet()` treats devoured players as dead (excluded from the alive count) to prevent a scenario where the Pelican is the only killer alive but has a full stomach — soft-lock prevention.

## Design decisions

- **Devoured stay alive until the meeting.** Not immediately dead — allows them to be released if the Pelican dies, matching user intent.
- **Excluded from alive count.** Prevents the soft-lock where a Pelican with a full stomach is technically the only killer but can't win.
- **No in-game indicator to other players.** Unlike Swooper, other players don't see an outline. Only the devoured and the informed dead know.
- **Position sync runs every tick, not via RPC.** Since movement is client-authoritative, every devoured player's own client moves them. Reduces network traffic.
- **Digestion uses `Exiled()`.** Meeting deaths leave no body — the victims were digested, there's nothing to find. (Mid-round release doesn't kill at all; the devoured walk away alive.)

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed.
- Role icon and ability sprite are placeholder art.
- Devoured players' camera-follow behavior (pinning to Pelican position, third-person spectation feel) needs verification — especially edge cases like the Pelican entering a vent or using a ladder.
- Soft-lock prevention via win-condition logic should be confirmed by testing a round where the Pelican devours multiple players.
- Edge case: if a devoured player's Pelican disconnects mid-round, the devoured player remains stuck (Pelican reference becomes null). Should test disconnection scenarios.
