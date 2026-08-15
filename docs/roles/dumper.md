# Dumper

Impostor Support role. Store the nearest dead body with the Store ability (on the **secondary**
keybind, since the Dumper keeps the vanilla kill button on Primary): the body is completely hidden — its
renderers *and* the dead player's lingering pet (not visible or reportable, unlike TOU-Mira's Undertaker,
which drags the body along visibly) — for a configurable store duration (default 20s). The body can be
dumped early at any time by pressing the button again (it toggles Store/Dump), or it auto-dumps —
revealed at the Dumper's current position — when the duration runs out, a meeting is called, or the
Dumper dies.

Files: `Roles/Impostor/DumperRole.cs`, `Buttons/Impostor/DumperCarryButton.cs`,
`Modifiers/DumperCarryModifier.cs`, `Events/DumperEvents.cs`, `Options/Roles/Impostor/DumperOptions.cs`.

## How it works

**Not `CarriedModifier`.** That family (`Modifiers/CarriedModifier.cs`, used by Pelican/Daddy Hagrid)
carries a *living player* as the modifier's own `Player`. A dead body is a plain `DeadBody` scene
object, not a `PlayerControl`, so it can't host a modifier itself — `DumperCarryModifier` lives on the
**Dumper** (the carrier) instead, storing the held body's `ParentId` and looking it up via MiraAPI's
`Helpers.GetBodyById` each time it needs it.

**Hide/reveal is a snap, not a fade.** `OnActivate` zeroes every `body.bodyRenderers[]` alpha and
disables `body.myCollider` (stops it being reportable/clickable) — modeled on TOU-Mira's Janitor clean
technique (`TownOfUs/Utilities/Extensions.cs` `CoCleanCustom`), but instant rather than the 1-second
fade that method also does, since a fade reads as "the body decaying," the wrong signal for "picked
up." **It also hides the dead player's pet** — a separate scene object that lingers at the death spot,
not one of `bodyRenderers`, so hiding the body alone left the pet sitting there. `MiscUtils.PlayerById(body.ParentId).cosmetics.TogglePet(false)`
hides it (the same toggle `MiscUtils.RemovePet` uses); the modifier's `FixedUpdate` re-asserts the hide
each tick since the game can re-show a pet on its own. `OnDeactivate` reverses all of this and
repositions the body to the Dumper's *current* location — not the pickup spot — before revealing it.
Because the body is fully hidden and non-colliding the whole time it's carried, nobody can see it move,
so there's no need for a per-frame follow like Undertaker's drag — a single teleport on drop is enough.
Pet-visibility restore is tracked by the dead player's id independently of the `DeadBody` object itself
(a dedicated `SetPetVisible` step, not folded into the body-reveal logic), so it still runs even if
another ability (Mafia Janitor's clean, Vulture's eat) destroys the carried body out from under the
Dumper mid-carry — the pet no longer stays stranded hidden just because there's no body left to reveal.

**The store duration is the button's cancellable EFFECT, which is what shows the countdown.**
`DumperCarryButton` sets `EffectDuration => DumperOptions.CarryDuration` and `IsEffectCancellable() =>
true`. Storing goes through `base.ClickHandler()`, which (after `OnClick` adds the modifier) sets
`EffectActive = true; Timer = EffectDuration` — so MiraAPI's `FixedUpdateHandler` draws the fill-up
countdown (`SetFillUp(Timer, EffectDuration)` + the timer text) while the body is stored, and the button
stays lit so it can be dumped early mid-countdown. This replaced a manual `storedTime`-in-`FixedUpdate`
timer that dropped the body correctly but showed **no countdown** (and, before that, a `TimedModifier`
that was observed to not expire at all). `DumperCarryModifier` stays a plain `BaseModifier`: the button's
effect timer is the single timing authority (the Dumper's own client owns it and broadcasts the removal),
so it's deterministic without each client racing an independent timer. This is the same RC-XD
Deploy/Detonate effect shape — a duration you watch tick down and can cut short — see
`docs/roles/daddy-hagrid.md`, which uses it identically for the cloak.

**Dump paths all funnel through `OnEffectEnd`.** The store's terminal action (revealing the body via
`RpcRemoveModifier<DumperCarryModifier>`) lives in `OnEffectEnd`, reached both when the effect times out
(auto-dump at the duration) and when a mid-store press cancels it (`ResetCooldownAndOrEffect`). `OnClick`
is store-only. Since the base targeted `ClickHandler` has no cancellable-effect branch, the button
overrides `ClickHandler` to route a press-while-storing to the cancel instead of re-running `OnClick`
(the RC-XD pattern). A `0.3s` debounce guards one physical press dispatching twice (keybind + click, or
Proton key autorepeat) and instantly storing-then-dumping. A meeting or the Dumper's death remove the
modifier via its own `OnMeetingStart`/`OnDeath`; the button then notices the modifier is gone (past a
short sync-settle grace) and ends its own effect so the countdown stops and the cooldown starts.

**The carry cooldown is a *dump* cooldown, independent of the store duration.** It starts when the
effect ends — every dump path (early cancel, duration timeout, or the button ending the effect after a
meeting/death drop) sets `Timer = Cooldown` via the effect-end machinery — so it's always measured from
the drop, never the pickup.

**Button label self-heals every tick.** `Name` is a fixed "Store" expression (only read once at button
creation, like every other button in this codebase); `FixedUpdate` re-asserts the correct "Store"/"Dump"
label via `OverrideName` every tick based on `EffectActive`, so the label stays correct even when the
carry ends without a click (duration expiry, meeting, death).

**Meeting/death auto-drop** come from the modifier's own `OnMeetingStart()`/`OnDeath(DeathReason)`
overrides — both call `Player.RemoveModifier(this)`, the same one-line pattern every "carry/hide"
modifier in this codebase uses. Since the modifier lives on the Dumper, `OnDeath` fires on the Dumper's
own death automatically, no separate event handler needed. (The store-duration auto-dump is the one
drop path that does NOT live on the modifier — it's button-driven, see above.) **Disconnect** is the one
case none of that covers — a disconnect never fires `OnDeath` — so `Events/DumperEvents.cs` hooks
`PlayerLeaveEvent` and releases the carried body in place if the carrying Dumper disconnects, the same
pattern `PelicanEvents`/`KirbyEvents`/`DaddyHagridEvents` use for their own carry-disconnect cases.

**No new RPCs.** Store is `Target.RpcAddModifier<DumperCarryModifier>(nearestBody.ParentId)` (the
generic modifier-add RPC, same shape `JailedModifier`'s constructor-arg usage uses upstream). Both the
manual dump and the button-driven auto-dump are `PlayerControl.LocalPlayer.RpcRemoveModifier<DumperCarryModifier>()`.

## Design decisions

- **Body hidden globally, not just from the Dumper's own view.** Every client hides the same body the
  same way, since the modifier's state is synced — nobody can report or interact with a stored body.
- **Keeps the vanilla Impostor kill button.** Nothing about carrying conflicts with it, unlike the Mafia
  sub-roles' team-kill-coordination mechanic.
- **Only unreported bodies can be stored** (`IsTargetValid` checks `!target.Reported`), matching
  Undertaker's own targeting rule.
- **Freely re-storable.** A dumped body has no extra cooldown/lockout beyond the normal Store ability
  cooldown before it (or any other body) can be stored again.

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed — untestable solo (needs a body to store). Highest-value checks:
  storing hides both the body *and* the dead player's pet from every client, it can't be reported while
  hidden, dumping reveals both at the Dumper's *current* position (not the store spot), early dump works,
  the store duration actually ends and auto-dumps (the button-driven timing fix), and a meeting call, the
  Dumper's own death, or the Dumper disconnecting mid-carry all drop the body (and pet) correctly.
- Role icon and the Store/Dump button sprite have no dedicated art yet — both use
  `SuperSquadAssets.ImpostorPlaceholderIcon`/`ImpostorPlaceholderButton`.
- If two Dumpers exist in the same lobby, there's no coordination between them — either could store
  a body the other just dumped. Not expected to be a problem, but untested.
