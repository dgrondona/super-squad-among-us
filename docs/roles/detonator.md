# Detonator

Impostor Killing role. Attach a bomb to a nearby living player with the Attach ability (on the
**secondary** keybind, since the Detonator keeps the vanilla kill button on Primary). After a 5-second
arm delay (configurable), press the button again to detonate at any time — no auto-expiry otherwise —
killing the bombed player and anyone within a small radius of their **current** position (not the
position at attach time, since the target keeps moving). Only one bomb can be active at a time. The bomb
is removed with no detonation if a meeting is called before it goes off.

Files: `Roles/Impostor/DetonatorRole.cs`, `Buttons/Impostor/DetonatorAttachButton.cs`,
`Modifiers/DetonatorBombModifier.cs`, `Modules/SuperSquadDetonator.cs`,
`Options/Roles/Impostor/DetonatorOptions.cs`.

Design: adapted from TOU-Mira's Bomber (`reference/TOU-Mira/TownOfUs/Modules/Bomb.cs` — a stationary
timed charge) and this repo's own RC-XD (two-phase Deploy/Detonate button), combined to support a bomb
that follows a moving target instead of a fixed point.

## How it works

**The bomb is a marker `BaseModifier` on the target**, not a plain field on `DetonatorRole` — matching
every other "effect tied to a specific other player" in this codebase (`HexedModifier`,
`ElusiveShieldModifier`, `MedicShieldModifier`, `DragModifier`; none of them use a bare role-instance
field for the relationship). A modifier buys three things a field doesn't:

- **Free meeting/death cleanup** via `OnMeetingStart()`/`OnDeath()` overrides — "bomb removed if a
  meeting is called" falls out of the same one-liner every sibling modifier already uses.
- **A live-following bomb for free.** Because the modifier's `Player` *is* the target,
  `Player.GetTruePosition()` read at detonate time is inherently up to date — unlike TOU-Mira's Bomber
  or this repo's RC-XD, whose effects aren't tied to a moving `PlayerControl`, so they store a static
  plant position instead.
- **Trivial extensibility** if a visible indicator is ever wanted (currently `HideOnUi => true` — no
  tell to the target or bystanders, matching Witch's hex/Sniper's shot).

**Arm delay is a pure client-side gate**, not a server-validated one: `DetonatorAttachButton.CanUse()`
checks `Time.time - modifier.PlantedAt >= ArmDelay` before allowing the Detonate branch — the same
technique `RcXdDeployButton.DetonateArmDelay` uses to guard double-dispatch. No RPC-side re-validation
is needed since only the Detonator's own client can click their own button.

**Button toggles Attach/Detonate** by tracking the currently-bombed target in a private field
(`activeBomb`) — `UndertakerDragDropButton`'s toggle shape, not RC-XD's `EffectActive`/`EffectDuration`
machinery (which assumes a finite auto-expiring effect; Detonator explicitly has no auto-expiry other
than the meeting clear). **Detonate needs a `ClickHandler` override**: this is a targeted button
(`<DetonatorRole, PlayerControl>`), and `CustomActionButton<T>.CanClick()` (which the base `ClickHandler`
gates on) hard-requires a fresh nearby `Target` *and* `Timer <= 0`. After attaching, the attach cooldown
is running and there's usually no valid target under the cursor, so the Detonate press never registered
(this was the "doesn't get the detonate ability after attaching" bug). The override detonates directly
when `activeBomb` still carries the modifier, gated only by `CanUse()`'s arm-delay/can-act checks, then
starts a fresh cooldown — the same fix pattern as `DumperCarryButton`'s early drop. The label self-heals
every `FixedUpdate` tick based on whether `activeBomb` still actually carries the modifier, in case it
resolved itself (meeting, target's death) without a click here.

**Detonate — `SuperSquadDetonator.RpcDetonate`** — adapts Bomber's AoE-and-meeting-noop pattern
(`Bomb.CoDetonate`) to a live target position: computes `Helpers.GetClosestPlayers(target.GetTruePosition(), radius)`,
filters for alive/not-vented/not-`FirstDeadShield`/not-`DisabledModifier`-protected (same filter shape
RC-XD's detonate uses), respects `CanKillImpostors`, then `RpcSpecialMultiMurder(..., causeOfDeath: "SuperSquadDetonator")`.
The meeting-noop is double-covered: the modifier's own `OnMeetingStart` already removes it the instant a
meeting starts, and the RPC handler's own `MeetingHud.Instance` guard is a second line of defense
against a same-frame race.

## Design decisions

- **Only one bomb active at a time.** Attaching while a bomb is already out isn't offered as a target
  (`GetTarget` excludes anyone already `HasModifier<DetonatorBombModifier>()`), and the button itself
  tracks a single `activeBomb`.
- **No visible indicator to the target or bystanders.** Stealth, matching Witch's hex and the Sniper's
  shot — no in-game tell besides the kill itself.
- **Detonation is instant**, no warning beat before the kill resolves — matches RC-XD/Bomber.
- **The explicit target is filtered the same as everyone else in the blast**, including the
  `CanKillImpostors` check — no special-casing an impostor-aligned primary target to always die
  regardless of that option.

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed — untestable solo (needs a second player to bomb). Highest-value
  checks: Detonate is blocked for the first 5s after Attach, detonating after arming kills the target
  and anyone who has since walked into the small radius around the target's *current* (not plant-time)
  position, and calling a meeting before detonating clears the bomb with no explosion.
- Role icon and the Attach/Detonate button sprite have no dedicated art yet — both use
  `SuperSquadAssets.ImpostorPlaceholderIcon`/`ImpostorPlaceholderButton`.
- Cause of death (`DiedToSuperSquadDetonator` = "Detonated") hasn't been confirmed to display correctly
  in a real game.
