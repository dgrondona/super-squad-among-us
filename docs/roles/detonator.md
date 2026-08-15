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

**Arm delay is the button's own `Timer`, shown as a visible countdown.** Attaching calls
`SetTimer(ArmDelay)`, so the button shows a plain cooldown-style countdown (greyed, ticking down) for
those seconds — the "show a 5s cooldown on attach" the design calls for — and the bomb is armed
(`Armed => activeBomb != null && Timer <= 0f`) the instant that Timer elapses. Driving the arm this way
means the gate never depends on the synced modifier having reached any client, *and* it produces the
countdown for free. `ArmDelay`'s minimum (2s) is far longer than the modifier sync window, so
"Timer elapsed" doubles as "the modifier has certainly synced" for the stale-clear below. No RPC-side
re-validation is needed since only the Detonator's own client can click their own button.

**Timing: Attach → visible arm countdown → Detonate at any time → *then* the re-attach cooldown
starts.** Attaching starts only the arm countdown (`Timer = ArmDelay`), not a real cooldown; the
"waiting to detonate" window after it runs no cooldown either. The re-attach cooldown (`Timer =
AttachCooldown`) starts only once the bomb is actually detonated — the same "cooldown measured from the
terminal action" shape as the Dumper's dump cooldown. The label reads **"Attach"** (greyed, counting
down) while arming and flips to **"Detonate"** only once armed, so the sequence a player sees is
"attach → a cooldown → the Detonate button appears → a normal attach cooldown".

**Button toggles Attach/Detonate** by tracking the currently-bombed target in a private field
(`activeBomb`) — `UndertakerDragDropButton`'s toggle shape rather than RC-XD's cancellable-effect
machinery (the arm is a *disabled* wait, not a usable effect, so a plain `Timer` fits it — unlike the
Dumper/Hagrid, whose durations are cancellable effects). `activeBomb` is the **local source of truth**
for the Attach-vs-Detonate phase; it is deliberately NOT re-derived from
`target.HasModifier<DetonatorBombModifier>()` each frame. A freshly-sent `RpcAddModifier` is only
*queued* onto the target's `ModifierComponent` and isn't visible via `HasModifier` until that
component's next `FixedUpdate`, so a phase driven by `HasModifier` flipped the label for a tick right
after attaching (the "detonate button flashes then reverts" bug). `activeBomb` is instead cleared only
on reliable signals: the target's death, a meeting starting (both also remove the synced modifier), or —
once the arm countdown has elapsed (so the modifier has certainly synced) — the modifier genuinely being
gone (external removal or stale cross-game state). **Detonate needs a `ClickHandler` override**: this is
a targeted button, and the base can-click gate hard-requires a fresh nearby `Target` *and* an elapsed
timer, which a detonate press never has (after attaching there's usually no valid target under the
cursor) — this was also part of the "doesn't detonate after attaching" bug. The override attaches
(starting the arm `Timer`) and detonates directly (gated only by `CanUse()`'s armed/can-act checks),
setting `Timer = Cooldown` only on that detonation. A `0.3s` debounce guards one physical press
dispatching twice. Both branches log to BepInEx so a "detonate did nothing" report is diagnosable.

**Detonate — `SuperSquadDetonator.RpcDetonate`** — adapts Bomber's AoE-and-meeting-noop pattern
(`Bomb.CoDetonate`) to a live target position: computes `Helpers.GetClosestPlayers(target.GetTruePosition(), radius)`,
filters for alive/not-vented/not-`FirstDeadShield`/not-`DisabledModifier`-protected (same filter shape
RC-XD's detonate uses), respects `CanKillImpostors`, then
`RpcSpecialMultiMurder(..., MeetingCheck.OutsideMeeting, ..., causeOfDeath: "SuperSquadDetonator")`.
The meeting-noop has two independent layers: the modifier's own `OnMeetingStart` removes the bomb
locally the instant a meeting starts, and the kill is sent with an explicit `MeetingCheck.OutsideMeeting`
(not the `List<PlayerControl>` overload's implicit `MeetingCheck.Ignore` default), so every receiving
client re-checks its own meeting state before applying the kill — not just the sender's local
`MeetingHud.Instance` guard, which alone can't stop a remote client already on the meeting screen from a
report/detonate RPC race. Validation checks bomb ownership (`DetonatorBombModifier.Detonator ==
source`), not just ability-holding — the kit is borrowable via AbilityGrants, so ability-holding alone
doesn't imply the sender placed *this* bomb once more than one bomb-holder can exist at once.

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
