# Pelican

Neutral Killing role. Devour nearby players with the primary ability button. Devoured players are hidden from the round and pinned to follow the Pelican's position (their camera spectates the Pelican). At the start of the next meeting, all devoured players die with no bodies. If the Pelican is killed mid-round, everyone in its stomach is released alive at the spot it died. Devoured players don't count as alive for the Pelican's win condition — preventing a soft-lock from a full stomach.

Files: `Roles/Neutral/PelicanRole.cs`, `Buttons/Neutral/PelicanDevourButton.cs`, `Modifiers/DevouredModifier.cs`, `Modifiers/DevouredDisabledModifier.cs`, `Events/PelicanEvents.cs`, `Options/Roles/Neutral/PelicanOptions.cs`.

Design: adapted from AllTheRoles per the user's own spec; see `docs/porting/README.md`.

## How it works

**Devour button.** Targeted ability on primary action, range-based like a vanilla kill button. Target any player, impostors included (neutral killer).

**Devoured modifier.** `DevouredModifier` (synced via `RpcAddModifier`) hides the player and freezes movement. While devoured, the player is pinned to the Pelican's position every `FixedUpdate` — their camera follows the Pelican. No in-game indicator except to the devoured player and the informed dead (faint outline). The freeze follows TOU-Mira's Ambusher pattern (owner-only `moveable = false` + `MyPhysics.ResetMoveState()` + `NetTransform.SetPaused(true)`), so the per-tick pin is the only position source on every client — no fight with NetTransform broadcasts. Appearance self-heals per tick like the invisibility roles.

**Incapacitation via `DevouredDisabledModifier`.** A companion TOU-Mira `DisabledModifier` (added/removed locally by `DevouredModifier` on each client) with `CanBeInteractedWith`/`CanUseAbilities`/`CanReport` all false. TOU-Mira's `ButtonClickPatches` and targeting utilities consume these, so a devoured player can't report bodies the Pelican walks past, can't use abilities/consoles/map, and can't be targeted by anyone's kill/ability buttons while in the stomach (which would otherwise let an impostor "kill the invisible passenger" standing on the Pelican). The Sniper's line-shot honors the same flag.

**Meeting digestion.** `PelicanEvents.StartMeetingEventHandler` runs before vote areas are built: every devoured player dies via `Exiled()` (exile-style, no body). Runs deterministically on every client since devoured state arrived via synced modifiers.

**Mid-round release.** `PelicanEvents.PlayerDeathEventHandler` runs when the Pelican dies: every devoured player is released with a position snap via `RpcSnapTo` (client-authoritative). Devoured player's own client performs the snap. **Disconnect release:** `PlayerLeaveEventHandler` releases the stomach in place if the Pelican disconnects (a disconnect never fires `PlayerControl.Die`, so without this the devoured would stay pinned and hidden forever).

**Win condition excludes devoured.** `PelicanRole.WinConditionMet()` treats devoured players as dead — excluded from the alive count AND subtracted from `MiscUtils.KillersAliveCount` (that utility counts devoured players since they're technically alive; without the subtraction a devoured rival killer would block the Pelican's win forever — fixed 2026-07-16). Known accepted gap: a devoured power-crew killer with CrewKillersContinue on still counts.

**Devour respects Veteran alert.** The devour button implements `IKillButton`, so TOU-Mira's Veteran counter-kill fires: devouring an alerted Veteran kills the Pelican instead. Intentional — matches every other kill button's contract.

## Design decisions

- **Devoured stay alive until the meeting.** Not immediately dead — allows them to be released if the Pelican dies, matching user intent.
- **Excluded from alive count.** Prevents the soft-lock where a Pelican with a full stomach is technically the only killer but can't win.
- **No in-game indicator to other players.** Unlike Swooper, other players don't see an outline. Only the devoured and the informed dead know.
- **Position sync runs every tick, not via RPC.** Since movement is client-authoritative, every devoured player's own client moves them. Reduces network traffic.
- **Digestion uses `Exiled()`.** Meeting deaths leave no body — the victims were digested, there's nothing to find. (Mid-round release doesn't kill at all; the devoured walk away alive.)

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed — untestable solo (needs a second player to devour). Highest-value checks: camera-follow feel while devoured, meeting digestion showing the victims as dead in the vote list, release position after killing the Pelican, and a devoured player being unable to report/use anything.
- Role icon and ability sprite are placeholder art.
- Devoured players' camera-follow behavior needs verification — especially edge cases like the Pelican entering a vent or using a ladder.
- Soft-lock prevention via win-condition logic should be confirmed by testing a round where the Pelican devours multiple players.
- Pelican disconnect now releases the stomach in place (`PlayerLeaveEventHandler`) — verify in a real lobby.
