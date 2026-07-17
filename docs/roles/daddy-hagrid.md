# Daddy Hagrid

Crewmate Protective role. Hide the nearest player in the cloak with the primary ability button: the
hidden player behaves exactly like a Pelican-devoured one (hidden, frozen, pinned to Hagrid,
untargetable, can't report/use anything) except that they pop back out **alive** — after the
configurable hide duration (default 15s) at Hagrid's position, or immediately when a meeting is
called. If Hagrid dies mid-hide, the player is released alive where he fell; a disconnect releases
them in place.

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

**Timed release.** Unlike the indefinite devour, `CloakHiddenModifier` overrides `AutoStart => true`
and `Duration` from options, so MiraAPI's `TimedModifier` timer removes it on every client when the
hide runs out (state arrived via `RpcAddModifier`, timers tick per client — same accepted skew as the
invisibility roles).

**Release positioning.** One owner-side `RpcSnapTo(Player.GetTruePosition())` in
`CloakHiddenModifier.OnDeactivate` covers every release path (timer pop-out, Hagrid death,
disconnect): the per-tick pin already placed each client's copy at the carrier's last position, so
re-broadcasting it re-syncs the just-unpaused NetTransform. Skipped during meetings — vanilla
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
- **Button effect mirrors the hide duration** (Veteran-alert shape): fill-up while someone is
  hidden, then the cooldown starts.

## Not yet verified in-game / known follow-ups

- Needs 2-client verification: hide/pop-out feel, camera-follow while hidden, meeting mid-hide
  releasing alive and voting normally, Hagrid death/disconnect releases, and the Pelican regression
  pass (devour, meeting digestion, death release).
- No dedicated art — role icon uses the generic Crewmate placeholder
  (`Resources/Placeholders/Crewmate.png`); the Hide button temporarily reuses TOU-Mira's Time Lord
  Rewind sprite (`TouCrewAssets.RewindSprite`) until a dedicated placeholder exists.
- If Hagrid himself gets devoured by a Pelican mid-hide, his passenger stays hidden until their own
  timer releases them at Hagrid's pinned position (inside the stomach ride). Weird but harmless;
  revisit if it plays badly.
