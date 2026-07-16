# Sniper

Impostor Killing role. Press the secondary ability button to shoulder the rifle and enter aiming mode. While aiming, a guide sprite points from your body toward your cursor. Click anywhere in the world to fire a piercing bullet that goes through walls and kills everyone within 0.2 units of the bullet's path. Aiming lasts for a configurable window (default 10s); if you don't fire, the aim times out and the cooldown begins. The bullet can optionally be invisible to other players (configurable).

Files: `Roles/Impostor/SniperRole.cs`, `Buttons/Impostor/SniperSnipeButton.cs`, `Modules/SniperShots.cs`, `Options/Roles/Impostor/SniperOptions.cs`.

Design: adapted from AllTheRoles per the user's own spec; see `docs/porting/README.md`.

## How it works

**Aim-window pattern.** Press the button to shoulder and enter aiming mode; stays armed until you fire or the aim window expires. No re-click needed, just click in the world to fire. Fires on left-click input (not the HUD button again).

**Hit detection is line-based.** `SniperShots.FindHits` casts a ray from origin toward direction and checks perpendicular distance to the line for each living player. If ≤ 0.2 units (ATR's hit-width), they're hit. Pierces clusters (not closest-target-only). Ignores the Sniper themself and, if configured, fellow impostors.

**Aim guide sprite.** Updates every frame to point from the Sniper's position toward the cursor; destroyed when aiming ends. Used `atan2` to compute rotation angle.

**Bullet visual.** Either RPC-synced (`SniperShots.RpcShowShot`, all clients see it) if `BulletVisibleToOthers` is on, or shown locally to the Sniper only. Visual flies for 60 units at 40 units/second (purely cosmetic; the kill line is infinite). Multi-kill resolution uses `RpcSpecialMultiMurder` (TOU-Mira utility) to kill all victims on one RPC.

**UI-click guard.** `EventSystem.IsPointerOverGameObject()` blocks fire if the pointer is over HUD. Map-open also blocks fire. Prevents accidental kills into buttons.

## Design decisions

- **Bullet invisible to others by default.** Behind `BulletVisibleToOthers` toggle — the Sniper can remain hidden. Only the victims and the Sniper know where the shot came from.
- **Piercing line, not cone.** Uses perpendicular-distance-to-line, not ATR's closest-target cone. Allows the Sniper to pierce clusters.
- **Aim window is timed, not toggle.** Pressing doesn't toggle aim on/off, just opens the window. Time pressure adds risk/reward.
- **Firing always ends aim and starts cooldown.** Regardless of whether any targets were hit. Prevents spam.
- **UI-click guard prevents accidental fires.** Especially important since the Sniper needs to aim at world positions, not click the button again.

## Not yet verified in-game / known follow-ups

- Manual in-game verification needed.
- Role icon and ability sprite are placeholder art.
- UI-click guard using `EventSystem.IsPointerOverGameObject()` needs IL2CPP verification — determine whether it correctly detects clicks on Among Us's IL2CPP HUD.
- Aim guide rotation and positioning should be visually confirmed in a real game (current math is standard `atan2` projection, magnitude 0.6 from the Sniper).
- Edge case: if the Sniper enters a meeting during aim mode, aim cancels and cooldown starts — intended behavior, should be confirmed.
- Z-depth sorting (`position.y / 1000f - 1f`) should be verified to ensure the bullet and guide render correctly relative to the environment.
