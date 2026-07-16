# Astral

Impostor Support role. Click the secondary ability button to enter ghost form: become invisible, walk through walls, and escape dangerous situations. After the duration expires, snap back to the phased spot and linger invisible for a few seconds before fully returning. The form cannot be cancelled early once activated.

Files: `Roles/Impostor/AstralRole.cs`, `Buttons/Impostor/AstralFormButton.cs`, `Modifiers/AstralFormModifier.cs`, `Modifiers/AstralLingerModifier.cs`, `Modifiers/TimedInvisibilityModifier.cs`, `Options/Roles/Impostor/AstralOptions.cs`, `Events/AstralEvents.cs`.

Design: adapted from AllTheRoles per the user's own spec; see `docs/porting/README.md`.

## How it works

**Two-phase modifier handoff.** `AstralFormModifier` (ghost phase) and `AstralLingerModifier` (grace phase) both inherit from `TimedInvisibilityModifier`, which handles shared invisibility rendering. Ghost phase disables the player's collider on activation, then re-enables it and snaps the player back via `RpcSnapTo` on deactivation (owner-only; client-authoritative). The linger modifier is added **locally on every client** inside the form's `OnDeactivate` — each client's form timer expires on its own (`TimedModifier` counts down per client), so a local add makes the handoff gapless. It was originally an owner-side `RpcAddModifier`, which opened a latency window where remote clients rendered the astral fully visible between the phases. The button's `EffectDuration` equals form + linger time, so the cooldown doesn't start until both phases end (button-vs-modifier clock drift is bounded by a tick; the button's `OnEffectEnd` cleanup only fires on real disagreements).

**Collider self-heal.** `AstralFormModifier.FixedUpdate` re-disables `Player.Collider` every tick instead of only once in `OnActivate` — killing someone while phased (the vanilla kill animation, `CustomMurder`/`CoPerformCustomKill`) re-enables the collider mid-animation, which used to solidify the astral and end wall-passing early even though the modifier (and invisibility) was still active. Same self-heal pattern already used for appearance/`Visible` above; see `docs/il2cpp-gotchas.md`.

**Die-without-kill option.** `AstralOptions.DieWithoutKill` (off by default). `Events/AstralEvents.cs` subscribes to `AfterMurderEvent` (fires on every client for every murder, not just the killer's own) and sets `HasKilled = true` on the killer's active `AstralFormModifier`, if any. If the option is on and the phase ends with `HasKilled` still false, `OnDeactivate` kills the astral instead of adding the linger — snap-back happens first so the body doesn't spawn wherever the phase happened to end (potentially inside a wall), then the death resolves locally on every client via `DeathHandlerModifier.UpdateDeathHandlerImmediate` + `PlayerControl.CustomMurder(self, self, ...)`, the same self-inflicted-death pattern `LoverEvents` uses for heartbreak (no RPC needed: `HasKilled` is already synced everywhere via `AfterMurderEvent`, and every client's phase timer expires in lockstep). Death cause locale key: `SuperSquadDiedToAstral` ("Faded").

**Shared invisibility rendering.** Both phases use `TimedInvisibilityModifier`, which implements Swooper's viewer rule: the astral themself, fellow impostors, and the informed dead see a faint outline (0.1α black); everyone else sees nothing. Two per-tick self-heals in `FixedUpdate`: `Player.Visible` is re-asserted (vanilla flips it back on across vent/ladder animations, and cameras honor it), and the appearance is re-applied whenever `GetAppearanceType()` is no longer `Swooper` (anything that reset the player's look — e.g. the phase handoff ordering, where the ending phase's `ResetAppearance` can land after the next phase's `RawSetAppearance` — is undone within one tick).

**Syncing.** Modifiers sync via `RpcAddModifier` (MiraAPI standard); movement uses `RpcSnapTo` (TOU-Mira standard). No custom RPCs.

## Design decisions

- **Two-phase structure** isolates ghost mechanics (collider disable, wall-pass) from linger visibility (collision re-enabled but invisible) — clean separation of concerns and clearer flow.
- **Cannot cancel early.** Once phased, the tether runs its course. Prevents abuse of the movement immunity.
- **Linger phase deliberate.** Returning is a vulnerable moment; linger gives the Astral cover for that split second, making it less obvious you just phased.
- **No custom RPCs.** Movement and state arrive via standard TOU-Mira channels (`RpcSnapTo`, `RpcAddModifier/RemoveModifier`), keeping the implementation simple.
- **Die-without-kill is opt-in and off by default.** Raises the stakes of phasing (use it or lose it) without changing the default experience.

## Playtest history

- **2026-07 first playtest:** core loop (phase, wall-walk, snap-back) reported working. The tester reported "crewmates see the outline and can chase the astral". Analysis: on crewmate clients the code sets BOTH `RendererColor = Color.clear` AND `Player.Visible = false` — the faint outline is only ever built for the astral themself, impostor-aligned viewers, and the informed dead (`TheDeadKnow` on), matching TOU-Mira's Swooper exactly (`SwoopModifier.GetVisualAppearance`). The likely explanations for the sighting: the observing account was the astral's own view (owner always sees their outline), an impostor teammate, or a dead player with The Dead Know enabled. Two real handoff bugs that could flash the astral visible on remote clients WERE found and fixed (RPC-latency linger gap; ResetAppearance-after-RawSetAppearance ordering — both above). **Needs a re-test observing from a living crewmate's client**; if crew still see an outline there, capture which client observed it and what the two invisibility phases showed.
- **2026-07-16 second playtest: "astral loses the ability to phase after killing".** Confirmed and fixed — see Collider self-heal above. Also requested and added: die-without-kill option (off by default).

## Not yet verified in-game / known follow-ups

- Re-test crew-view invisibility from a genuine crewmate client (see Playtest history).
- Re-test killing mid-phase: confirm wall-passing now survives a kill until the phase's own timeout, and confirm `DieWithoutKill` correctly triggers a self-death when no kill happens (and correctly does NOT trigger one when a kill does happen).
- Role icon and ability sprite are placeholder art — swap out when real art exists.
- If the astral dies mid-phase inside a wall (e.g. `DieWithoutKill` triggers while still off any snap-back path — shouldn't happen since snap-back runs first, but worth double-checking), the body may be unreachable/unreportable.
