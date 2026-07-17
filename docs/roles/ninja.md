# Ninja

Impostor Killing role. Two-stage ability (TOR convention): press to mark the closest target (armed after a 5-second delay), press again to teleport to and kill them instantly from anywhere on the map. Assassination leaves visual traces at both launch and landing spots; the Ninja optionally turns invisible for a few seconds after to slip away. Mark is local to the Ninja's client only (tracking arrow invisible to others and cameras), but assassination syncs across all clients.

Files: `Roles/Impostor/NinjaRole.cs`, `Buttons/Impostor/NinjaMarkButton.cs`, `Modifiers/NinjaMarkedModifier.cs`, `Modifiers/NinjaInvisibleModifier.cs`, `Modules/NinjaTraces.cs`, `Options/Roles/Impostor/NinjaOptions.cs`.

Design: ported from TheOtherRoles; see `docs/porting/README.md`.

## How it works

**Two-click button pattern.** `NinjaMarkButton` toggles between mark and assassinate phases. Mark state is button-local only (`Marked` field on the button, never synced). Arming delay is fixed at 5 seconds per TOR. The `ClickHandler` override replicates the `GlitchHackedModifier`/`DisabledModifier` gating from `TownOfUsButton.ClickHandler` — any button that overrides `ClickHandler` must re-add those checks or it can be used while hacked/incapacitated.

**Local-only arrow.** The tracking arrow (`NinjaMarkedModifier`) is added directly to the marked target's local modifier list, not via RPC — only the Ninja's client sees it. Removes itself on meeting start or target death. Scaled to 75% of TOU-Mira's shared arrow prefab size (`ArrowTargetModifier.CreateArrow` doesn't expose scale as a parameter, so `NinjaMarkedModifier` captures the prefab's natural scale on `OnActivate` and re-applies `natural * 0.75` every `FixedUpdate` in case `ArrowBehaviour.Update()` resets it) — the shared prefab read as too large for a persistent tracking indicator.

**Assassination sequence.** Deterministic order: launch trace RPC → add invisibility modifier RPC → murder RPC → landing trace RPC. This ensures visual consistency across clients. Assassination works from anywhere (no range check on the second click).

**World traces.** Ninja traces are real scene GameObjects placed via `NinjaTraces.RpcPlaceNinjaTrace`, so every client renders them and they appear in admin camera feeds. Each trace animates: starts at a tint color, lerps toward green over the color-fade duration, then alpha-fades at the end of the trace lifetime. The sprite is loaded at 225 pixels-per-unit (`SuperSquadImpAssets.NinjaTraceSprite`) — TOR's exact value (`Helpers.loadSpriteFromResources(..., 225f)`); the port originally omitted the PPU argument (defaulting to MiraAPI's 100), rendering traces 2.25x too large.

**Trace tint is not the Ninja's player color.** TOR's `NinjaTrace` looks like it tints toward the Ninja's actual outfit color (`Palette.PlayerColors[ColorId]` is computed) but immediately overwrites that with `Helpers.isLighterColor(Ninja.ninja) ? Color.white : Palette.PlayerColors[6]` — and `isLighterColor` is just `playerId % 2 == 0`, unrelated to the player's actual color despite the name. The start color is genuinely white or black based on player-ID parity, nothing else. `NinjaTraces.cs` replicates this exactly rather than "fixing" it to use the real outfit color, per the "keep it faithful to TOR" instruction.

**Post-assassination invisibility.** `NinjaInvisibleModifier` inherits from `TimedInvisibilityModifier` (same as Astral), using Swooper's faint-outline rule for impostors and dead players.

## Design decisions

- **Arrow is Ninja-local only.** Only the Ninja sees it; the kill itself is the tell to other players. Keeps the Ninja's planning hidden.
- **Traces are world objects, not client-only UI.** They appear in admin feeds and provide a forensic trail post-kill. Other players can see the Ninja dashed through here.
- **Mark clears on meeting start, target death, or ninja death.** Meeting ejection is a natural reset point; dead targets can't be killed again; a dead ninja's arrow disappears (TOR parity).
- **No assassinating from or into vents.** TOR gates the strike on the ninja's `CanMove` and on the target not being vented; we mirror both in `CanUse()` (parity fix, 2026-07-16).
- **Trace fade matches TOR exactly:** 1s fade-out, shrunk to half the lifetime for sub-1s traces.
- **Fixed 5s arming delay.** Per TOR. Prevents instant mark→kill from the button cooldown alone.
- **Multi-mark edge case:** if another player walks closer during the arm window, the mark does not auto-retarget. The Ninja must manually re-mark. (TOR behavior.)

## Playtest history

- **2026-07-16 second playtest: "traces' scaling and coloring is off".** Both confirmed against TOR source and fixed — see World traces / trace tint above.
- **2026-07-16 second playtest: "arrow that points to the target [should be] a little bit smaller".** `NinjaMarkedModifier` now scales the tracking arrow to 75% of the shared prefab's size.

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed, including a re-test of the trace scale/color fix and the smaller tracking arrow.
- Role icon has no dedicated art yet — uses Town of Us: Mira's generic Impostor team icon (`Resources/Placeholders/Impostor.png`) as a stand-in; the old AI-generated icon was removed. Swap out when real art exists.
- Mark and assassination sound effects are missing. TOR's `warlockCurse` (mark) and `witchSpell` sounds are extractable from the TOR sound bundle; see `docs/porting/README.md`.
- Multi-target retargeting: test a scenario where the closest target changes mid-mark to confirm it clears as intended.
