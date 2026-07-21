# Daddy Hagrid

Crewmate Protective role. Hide the nearest player in the cloak with the primary ability button: the
hidden player behaves exactly like a Pelican-devoured one (hidden, frozen, pinned to Hagrid,
untargetable, can't report/use anything) except that they pop back out **alive** — after the
configurable hide duration (default 15s) at Hagrid's position, or immediately when a meeting is
called. **The button is a Hide/Release toggle: while someone is hidden Hagrid can press it again to
release them early** (one player at a time). If Hagrid dies mid-hide, the player is released alive
where he fell; a disconnect releases them in place.

Files: `Roles/Crewmate/DaddyHagridRole.cs`, `Buttons/Crewmate/DaddyHagridHideButton.cs`,
`Modifiers/CloakHiddenModifier.cs`, `Modifiers/CarriedModifier.cs` (shared base),
`Modifiers/CarriedDisabledModifier.cs`, `Events/DaddyHagridEvents.cs`,
`Options/Roles/Crewmate/DaddyHagridOptions.cs`.

## How it works

**Shared `CarriedModifier` base.** All the hidden/frozen/pinned/incapacitated mechanics were
extracted verbatim from the Pelican's `DevouredModifier` into the abstract `CarriedModifier`
(2026-07-17); `DevouredModifier` and `CloakHiddenModifier` are **sealed siblings** on top of it.
They must stay siblings: `PelicanEvents` iterates `GetActiveModifiers<DevouredModifier>()` and
`DaddyHagridEvents` iterates `GetActiveModifiers<CloakHiddenModifier>()` — subclassing one from the
other would make each catch the other's carried players. See `docs/roles/pelican.md` for the full
mechanics of the carried state.

**Timed release is the button's cancellable EFFECT, which is what shows the countdown.**
`DaddyHagridHideButton` sets `EffectDuration => DaddyHagridOptions.HideDuration` and
`IsEffectCancellable() => true`. Hiding goes through `base.ClickHandler()`, which sets
`EffectActive = true; Timer = EffectDuration` after `OnClick` adds the cloak — so MiraAPI's
`FixedUpdateHandler` draws the fill-up countdown while the player is hidden and the button stays lit to
release early. `CloakHiddenModifier` is therefore **no longer a self-expiring `TimedModifier`**: it's an
indefinite `CarriedModifier`, and the button's effect timer is the single timing authority (the Hagrid's
own client owns it and broadcasts the release via `RpcRemoveModifier<CloakHiddenModifier>` when the
effect ends). This is the exact RC-XD / Dumper effect shape — a duration you watch tick down and can cut
short. This replaced a version that dropped correctly but showed **no countdown**.

**Hide/Release toggle + early release.** `EffectActive` is the Hide-vs-Release phase authority; a
`hiddenPlayer` local field just remembers *who* to release (deliberately not re-derived from
`HasModifier` each frame — a freshly-sent `RpcAddModifier` isn't visible on the target for a tick or two,
so a sync-settle grace guards the "cloak gone" detection). Both release paths — the effect timing out
and a mid-hide press cancelling it (`ResetCooldownAndOrEffect`) — funnel through `OnEffectEnd`, which
removes the cloak and starts the re-hide cooldown; `OnClick` is hide-only (the base targeted
`ClickHandler` has no cancel branch, so the button overrides it to route a press-while-hiding to the
cancel, the RC-XD pattern). A meeting or the Hagrid's death pop the player out via the modifier/events;
the button then notices the cloak is gone and ends its own effect. Because the button tracks one
`hiddenPlayer`, Hagrid hides one player at a time (must release before hiding another). `MaxUses` is
enforced by the base `CanUse`'s own uses check, decremented in `base.ClickHandler`.

**Release positioning.** One owner-side `RpcSnapTo(Player.GetTruePosition())` in
`CloakHiddenModifier.OnDeactivate` covers every release path (effect-driven timed/early release, Hagrid
death, disconnect): the per-tick pin already placed each client's copy at the carrier's last position,
so re-broadcasting it re-syncs the just-unpaused NetTransform. Skipped during meetings — vanilla
repositions everyone for the vote anyway.

**Meeting = pop out alive.** `CloakHiddenModifier.OnMeetingStart` removes the modifier — this is the
whole Hagrid/Pelican difference. There is deliberately no meeting handler in `DaddyHagridEvents`;
the Pelican's digestion kill lives untouched in `PelicanEvents`.

**Not a kill button.** `DaddyHagridHideButton` does NOT implement `IKillButton` — hiding someone
isn't an attack. TOU-global rules still apply though: clicking an alerted Veteran retaliates
(`MiraButtonClickEvent` interception is button-agnostic).

**No cross-stealing.** Both the devour button and the hide button exclude any
`HasModifier<CarriedModifier>()` target, so a Pelican can't devour a cloak-hidden player and Hagrid
can't hide someone already in a stomach.

## Design decisions

- **Targets anyone living, impostors included** — Hagrid doesn't know teams, and excluding impostors
  would confirm them by failure-to-target.
- **Hidden player keeps full Pelican-style incapacitation** — they're protected precisely because
  nothing can interact with them, and they can't act either (no reporting bodies Hagrid walks past).
- **Early release is a toggle, not a fixed-duration lock** — Hagrid can pull someone out before the
  duration expires (the button's cancellable effect); it auto-releases if he doesn't. One at a time.

## Not yet verified in-game / known follow-ups

- Needs 2-client verification: hide/pop-out feel, the fill-up countdown ticking down while someone is
  hidden and the button auto-releasing at zero, early-release mid-countdown, camera-follow while hidden,
  meeting mid-hide releasing alive and voting normally, Hagrid death/disconnect releases, and the Pelican
  regression pass (devour, meeting digestion, death release).
- No dedicated art — role icon uses the generic Crewmate placeholder
  (`Resources/Placeholders/Crewmate.png`); the Hide button temporarily reuses TOU-Mira's Time Lord
  Rewind sprite (`TouCrewAssets.RewindSprite`) until a dedicated placeholder exists.
- If Hagrid himself gets devoured by a Pelican mid-hide, his passenger stays hidden until Hagrid's own
  button effect times out (Hagrid is alive, just hidden, so his button keeps ticking) and releases them
  at his pinned position inside the stomach ride. Weird but harmless; revisit if it plays badly.
