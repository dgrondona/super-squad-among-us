# Dumper

Impostor Support role. Pick up the nearest dead body with the Carry ability (on the **secondary**
keybind, since the Dumper keeps the vanilla kill button on Primary): the body is completely hidden — its
renderers *and* the dead player's lingering pet (not visible or reportable, unlike TOU-Mira's Undertaker,
which drags the body along visibly) — for a configurable carry duration (default 20s). The body can be
dropped early at any time by pressing the button again, or it drops automatically — revealed at the
Dumper's current position — when the duration runs out, a meeting is called, or the Dumper dies.

Files: `Roles/Impostor/DumperRole.cs`, `Buttons/Impostor/DumperCarryButton.cs`,
`Modifiers/DumperCarryModifier.cs`, `Options/Roles/Impostor/DumperOptions.cs`.

## How it works

**Not `CarriedModifier`.** That family (`Modifiers/CarriedModifier.cs`, used by Pelican/Daddy Hagrid)
carries a *living player* as the modifier's own `Player`. A dead body is a plain `DeadBody` scene
object, not a `PlayerControl`, so it can't host a modifier itself — `DumperCarryModifier` lives on the
**Dumper** (the carrier) instead, storing the held body's `ParentId` and looking it up via
`FindObjectsOfType<DeadBody>()` each time it needs it (same technique `SuperSquadBodies.DestroyBodies`
already uses).

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

**Timed via `TimedModifier`, not `TownOfUsRoleButton.EffectDuration`.** `DumperCarryModifier : TimedModifier`
with `Duration` from `DumperOptions.CarryDuration` and `AutoStart => true` — the same self-expiring-on-
every-client mechanism `CloakHiddenModifier`/`ElusiveShieldModifier`/`SwoopModifier` already use. This
was chosen over the button's own `EffectDuration`/`OnEffectEnd` machinery (RC-XD's shape), which assumes
a fixed auto-expiring effect on the button's own player — awkward to bend around "an object might get
released early too."

**Manual early drop** is `DumperCarryButton`'s toggle: `OnClick` checks
`PlayerControl.LocalPlayer.HasModifier<DumperCarryModifier>()` to decide Pickup vs. Drop, matching
`UndertakerDragDropButton`'s shape rather than RC-XD's `EffectActive` cancellable-effect branch (which
assumes a fixed window, awkward for "may end early on demand"). **The drop needs a `ClickHandler`
override, not just a `CanUse()` special-case** — this is a targeted button (`<DumperRole, DeadBody>`),
and `CustomActionButton<T>.CanClick()` (which the base `ClickHandler` gates on) hard-requires a fresh
nearby `Target` *and* `Timer <= 0`. While carrying, the body is hidden and teleported out from under the
player (no valid target) — so the earlier `CanUse()`-only approach never actually let the drop click
through (this was the "premature drop doesn't work" bug). The override routes the drop straight through
`CanUse()` (which owns the carry-state/alive/hacked/disabled guards). The same fix pattern is used by
`DetonatorAttachButton`'s Detonate phase. A `0.3s` debounce guards against one physical press dispatching
twice (keybind + click, or Proton key autorepeat) and instantly picking-up-then-dropping.

**The carry cooldown is a *drop* cooldown, independent of the carry duration.** Neither pickup nor
carrying starts it; it begins the moment the carry actually *ends* — by early drop, duration expiry,
meeting, or death alike. Since auto-drops never route through the button, the button detects the
carry→not-carrying transition each `FixedUpdate` and sets `Timer = Cooldown` there, so every drop path
starts the cooldown identically. (Previously the cooldown ran from pickup, which conflated it with the
carry duration.)

**Button label self-heals every tick.** `Name` is a fixed "Carry" expression (only read once at button
creation, like every other button in this codebase); `FixedUpdate` re-asserts the correct "Carry"/"Drop"
label via `OverrideName` every tick based on the actual modifier state, so the label stays correct even
when the carry ends without a click (duration expiry, meeting, death).

**Meeting/death auto-drop** come from the modifier's own `OnMeetingStart()`/`OnDeath(DeathReason)`
overrides — both call `Player.RemoveModifier(this)`, the same one-line pattern every "carry/hide"
modifier in this codebase uses. Since the modifier lives on the Dumper, `OnDeath` fires on the Dumper's
own death automatically, no separate event handler needed.

**No new RPCs.** Pickup is `Target.RpcAddModifier<DumperCarryModifier>(nearestBody.ParentId)` (the
generic modifier-add RPC, same shape `JailedModifier`'s constructor-arg usage uses upstream). Manual
drop is `PlayerControl.LocalPlayer.RpcRemoveModifier<DumperCarryModifier>()`.

## Design decisions

- **Body hidden globally, not just from the Dumper's own view.** Every client hides the same body the
  same way, since the modifier's state is synced — nobody can report or interact with a carried body.
- **Keeps the vanilla Impostor kill button.** Nothing about carrying conflicts with it, unlike the Mafia
  sub-roles' team-kill-coordination mechanic.
- **Only unreported bodies can be picked up** (`IsTargetValid` checks `!target.Reported`), matching
  Undertaker's own targeting rule.
- **Freely re-pickable.** A dropped body has no extra cooldown/lockout beyond the normal Carry ability
  cooldown before it (or any other body) can be picked up again.

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed — untestable solo (needs a body to pick up). Highest-value checks:
  pickup hides both the body *and* the dead player's pet from every client, it can't be reported while
  hidden, drop reveals both at the Dumper's *current* position (not the pickup spot), early drop works
  (the fix above), and a meeting call or the Dumper's own death both drop the body correctly.
- Role icon and the Carry/Drop button sprite have no dedicated art yet — both use
  `SuperSquadAssets.ImpostorPlaceholderIcon`/`ImpostorPlaceholderButton`.
- If two Dumpers exist in the same lobby, there's no coordination between them — either could pick up
  a body the other just dropped. Not expected to be a problem, but untested.
