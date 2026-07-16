# Sniper

Impostor Killing role. Press the secondary ability button to shoulder the rifle and enter aiming mode. While aiming, a guide sprite points from your body toward your cursor. Click anywhere in the world to fire a piercing bullet that goes through walls and kills everyone within 0.2 units of the bullet's path. Aiming lasts for a configurable window (default 10s); if you don't fire, the aim times out and the cooldown begins. The bullet can optionally be invisible to other players (configurable).

Files: `Roles/Impostor/SniperRole.cs`, `Buttons/Impostor/SniperSnipeButton.cs`, `Modules/SniperShots.cs`, `Options/Roles/Impostor/SniperOptions.cs`.

Design: adapted from AllTheRoles per the user's own spec; see `docs/porting/README.md`.

## How it works

**Aim-window pattern.** Press the button to shoulder and enter aiming mode; stays armed until you fire or the aim window expires. No re-click needed, just click in the world to fire. Fires on left-click input (not the HUD button again).

**Aiming and firing run per rendered frame, not on the button's FixedUpdate.** `Patches/SniperAimPatch.cs` (a `HudManager.Update` postfix) calls `SniperSnipeButton.HandleAimFrame()` every frame. This matters: MiraAPI button `FixedUpdate` runs on `PlayerControl.FixedUpdate`'s fixed tick, and `Input.GetMouseButtonDown` is only true during the one rendered frame of the press — polling it from the fixed tick drops most clicks at any frame rate above the tick rate. This was the primary reason the first playtest reported "sniper doesn't work". Same pitfall and same fix as the Apparater's map click (see docs/roles/apparater.md).

**Hit detection is line-vs-hitbox, not line-vs-point.** `SniperShots.FindHits` treats each player as a circle around their **body sprite bounds** (center + max extent, ~0.5u; collider-position fallback) and hits when perpendicular distance to the shot line ≤ 0.2 (bullet half-width) + that radius. The original line-vs-`GetTruePosition()` test whiffed unless the line passed within 0.2u of the feet — the second playtest bug. Pierces clusters (all hits, ordered by distance). Skips: the Sniper, dead/disconnected, players in vents, `FirstDeadShield` holders, players with a `DisabledModifier` that has `CanBeInteractedWith == false` (devoured, ambush-hidden, ...), and — if configured — impostor-aligned players (`IsImpostorAligned()`, which covers alliance modifiers too).

**Aim guide sprite.** Updates every rendered frame (via the same patch) to point from the Sniper's position toward the cursor; destroyed when aiming ends.

**Bullet visual.** Either RPC-synced (`SniperShots.RpcShowShot`, all clients see it) if `BulletVisibleToOthers` is on, or shown locally to the Sniper only. Visual flies for 60 units at 40 units/second (purely cosmetic; the kill line is infinite). Multi-kill resolution uses `RpcSpecialMultiMurder` (TOU-Mira utility) to kill all victims on one RPC; it handles Guardian Angel protection internally (first-death shields are the caller's job, handled in `FindHits`).

**UI-click guard, two layers.** Among Us HUD buttons are collider-based `PassiveButton`s that `EventSystem.IsPointerOverGameObject()` does NOT detect, so `IsClickOnHud()` also probes the UI layer under the cursor through `HudManager.Instance.UICamera` via `Physics2D.OverlapPoint`. Additionally, the frame the button was armed is recorded (`armedFrame`) so the arming click itself can never fire the shot, and firing is blocked while hacked/disabled (`GlitchHackedModifier`/`DisabledModifier`) and while the map is open.

## Design decisions

- **Bullet invisible to others by default.** Behind `BulletVisibleToOthers` toggle — the Sniper can remain hidden. Only the victims and the Sniper know where the shot came from.
- **Piercing line, not cone.** Uses perpendicular-distance-to-line, not ATR's closest-target cone. Allows the Sniper to pierce clusters.
- **Aim window is timed, not toggle.** Pressing doesn't toggle aim on/off, just opens the window. Time pressure adds risk/reward.
- **Firing always ends aim and starts cooldown.** Regardless of whether any targets were hit. Prevents spam.
- **UI-click guard prevents accidental fires.** Especially important since the Sniper needs to aim at world positions, not click the button again.

## Playtest history

- **2026-07 first playtest: "sniper doesn't work".** Two root causes found and fixed: (1) click polling lived in the button's FixedUpdate and dropped most clicks (see above); (2) hit test was line-vs-center-point with a 0.2u corridor, so even registered shots whiffed. Both reworked — needs a re-test.

## Not yet verified in-game / known follow-ups

- Re-test after the click/hit-detection rework (see Playtest history).
- Role icon and ability sprite are placeholder art.
- Aim guide rotation and positioning should be visually confirmed (standard `atan2` projection, magnitude 0.6 from the Sniper).
- The UI-layer `Physics2D.OverlapPoint` probe assumes HUD button colliders live on the "UI" layer — confirm no HUD element is missed (clicking the kill button while aiming must not fire).
- Z-depth sorting (`position.y / 1000f - 1f`) should be verified to ensure the bullet and guide render correctly relative to the environment.
