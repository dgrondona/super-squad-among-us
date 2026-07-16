# Vulture

Neutral Evil role. Use a primary action button to consume a nearby dead body — the body is removed for all players and counts toward a win threshold (configurable 1–10 bodies, default 4). The Vulture wins when the threshold is met and they are still alive, ending the game instantly. While alive, blue arrows point toward all dead bodies on the map (toggleable via options, local to the Vulture only).

Files: `Roles/Neutral/VultureRole.cs`, `Buttons/Neutral/VultureEatButton.cs`, `Modules/SuperSquadBodies.cs`, `Options/Roles/Neutral/VultureOptions.cs`.

Design: ported from TheOtherRoles; see `docs/porting/tor-eraser-vulture.md`.

## How it works

**Eat button.** `VultureEatButton` is a primary-action button that targets the nearest dead body within range. Click to consume it via `SuperSquadBodies.RpcVultureEat`, which destroys the body GameObject on all clients and increments `VultureRole.EatenBodies` on every client.

**Eaten body count.** The count is tracked on every client (including the host). `VultureRole.WinConditionMet()` is called every frame by TOU-Mira's neutral win pipeline; when `EatenBodies >= BodiesNeededToWin`, the win triggers immediately and the game ends (TOR-style instant win, user decision per `docs/porting/README.md`).

**Body arrows.** `VultureEatButton.FixedUpdate` syncs blue arrows to every dead body on the map every frame, visible only to the Vulture. Arrows are created/destroyed as bodies appear and disappear (vanilla destroys bodies at meeting start; eats remove them mid-round). When the Vulture dies or enters a meeting, `SetActive` clears all arrows. Arrows respect the `ShowBodyArrows` option; when disabled, `ClearArrows` removes all arrows.

**Dead bodies only.** The Vulture can consume player corpses, not vanilla sabotage debris or other game objects. Bodies cleaned by TOU-Mira's standalone Janitor are gone before the Vulture can eat them, so they don't count toward the threshold — known gap.

**Win condition is frame-checked.** TOU-Mira's `NeutralRoleWinCondition` polls every active role's `WinConditionMet()` every frame. When the Vulture's returns true, the neutral win-condition triggers immediately, ending the game. No meeting required.

## Design decisions

- **Instant win on threshold.** TOR behavior — the game ends the moment the body count hits the target, anywhere on the map, even mid-task. No vote required; the Vulture's presence and food count are all that matter.
- **Win only if alive.** `WinConditionMet()` returns false if the Vulture has died, preventing a ghost Vulture from triggering a win.
- **Arrows sync every frame, not via RPC.** Since bodies are world state (visible to all clients), arrows are a UI-only convenience. Every client independently creates and destroys arrows per frame, reducing network traffic.
- **Arrows cleaned up properly.** When the Vulture dies, enters a meeting, or disables the option, arrows are destroyed to prevent visual clutter.
- **No default cooldown gating.** The eat button cooldown is configurable (default 15s, 10–60s + map bonuses). No use limit per meeting or per round.
- **Can vent.** By default `CanVent = true`; configurable via options, matching TOR's Vulture flexibility.

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed — untestable solo (needs bodies to eat and ideally a second player). Highest-value checks: eat button appears, targets the nearest body, removes it for all players, count increments, blue arrows appear and track all bodies, arrows disappear on death/meeting/option-off, and win triggers when threshold is met.
- Role icon and ability sprite are placeholder art.
- Sound effects for eating and win are missing; TOR's equivalent sounds are extractable (see `docs/porting/README.md`).
- Arrow positioning and size should be verified visually (ensure they point correctly and don't obscure gameplay).
- Verify interaction with TOU Janitor-cleaned bodies — confirm they don't count and don't spawn arrows after being cleaned.
- Verify win condition in a real game — instantaneous win on hitting the threshold mid-round or at meeting start.
