# Sniper

Impostor Killing role. Press the secondary ability button to shoulder the rifle and enter aiming mode — no aim assist is shown while deciding where to click; the projectile visual only appears at the moment you fire. Click anywhere in the world to fire a piercing bullet that goes through walls and kills everyone within 0.2 units of the bullet's path. Aiming lasts for a configurable window (default 10s); if you don't fire, the aim times out and the cooldown begins. The bullet can optionally be invisible to other players (configurable).

Files: `Roles/Impostor/SniperRole.cs`, `Buttons/Impostor/SniperSnipeButton.cs`, `Modules/SniperShots.cs`, `Options/Roles/Impostor/SniperOptions.cs`, `Patches/SniperVisionPatch.cs`.

Design: adapted from AllTheRoles per the user's own spec; see `docs/porting/README.md`.

## How it works

**Aim-window pattern.** Press the button to shoulder and enter aiming mode; stays armed until you fire or the aim window expires. No re-click needed, just click in the world to fire. Fires on left-click input (not the HUD button again).

**Aiming and firing run per rendered frame, not on the button's FixedUpdate.** `Patches/SniperAimPatch.cs` (a `HudManager.Update` postfix) calls `SniperSnipeButton.HandleAimFrame()` every frame. This matters: MiraAPI button `FixedUpdate` runs on `PlayerControl.FixedUpdate`'s fixed tick, and `Input.GetMouseButtonDown` is only true during the one rendered frame of the press — polling it from the fixed tick drops most clicks at any frame rate above the tick rate. This was the primary reason the first playtest reported "sniper doesn't work". Same pitfall and same fix as the Apparater's map click (see docs/roles/apparater.md).

**Hit detection is line-vs-hitbox, not line-vs-point.** `SniperShots.FindHits` treats each player as a circle around their **body sprite bounds** (center + max extent, ~0.5u; collider-position fallback) and hits when perpendicular distance to the shot line ≤ 0.2 (bullet half-width) + that radius. The original line-vs-`GetTruePosition()` test whiffed unless the line passed within 0.2u of the feet — the second playtest bug. Pierces clusters (all hits, ordered by distance). Skips: the Sniper, dead/disconnected, players in vents, `FirstDeadShield` holders, players with a `DisabledModifier` that has `CanBeInteractedWith == false` (devoured, ambush-hidden, ...), and — if configured — impostor-aligned players (`IsImpostorAligned()`, which covers alliance modifiers too).

**Shot origin is the shooter's sprite center, not their feet.** `PlayerControl.GetTruePosition()` sits at the collider/feet position, well below the visible body sprite — a line drawn from there looked like it started at the ground instead of the gun/chest. `SniperShots.GetShotOrigin()` reuses the same body-bounds lookup `FindHits` already uses for victims (`GetBodyCircle(player).Center`), so `Fire()`'s hit-test line and the bullet's visual start point both originate from the sprite's actual center.

**No aim guide.** Earlier versions rendered a persistent local arrow pointing from the Sniper's body toward the cursor for the whole aim window, using the same sprite as the bullet travel visual (`SniperGuideSprite`). Removed per playtest feedback (2026-07-16): the projectile placeholder should only be visible when the Sniper actually fires, not while still deciding where to aim. Direction is now computed purely from the cursor position at the instant of the click in `Fire()` — no visual feedback during the aim window itself.

**Bullet visual.** Either RPC-synced (`SniperShots.RpcShowShot`, all clients see it) if `BulletVisibleToOthers` is on, or shown locally to the Sniper only. Visual flies for 60 units at 40 units/second (purely cosmetic; the kill line is infinite). Multi-kill resolution uses `RpcSpecialMultiMurder` (TOU-Mira utility) to kill all victims on one RPC; it handles Guardian Angel protection internally (first-death shields are the caller's job, handled in `FindHits`).

**UI-click guard, two layers.** Among Us HUD buttons are collider-based `PassiveButton`s that `EventSystem.IsPointerOverGameObject()` does NOT detect, so `IsClickOnHud()` also probes the UI layer under the cursor through `HudManager.Instance.UICamera` via `Physics2D.OverlapPoint`. Additionally, the frame the button was armed is recorded (`armedFrame`) so the arming click itself can never fire the shot, and firing is blocked while hacked/disabled (`GlitchHackedModifier`/`DisabledModifier`) and while the map is open.

**Camera.main can be transiently null.** `Camera.main` does a tag-based scene lookup every call; TOU-Mira's own code defends against it being null in several places (e.g. the Sentry role's surveillance click-handler, `SentryCameraSurveillancePatch.cs`), which is exactly the same call pattern the Sniper's click-to-world conversion uses. `HandleAimFrame()` now checks `Camera.main == null` and bails before `Fire()` — since that leaves `EffectActive` true, the aim window stays open and the very next click (once `Camera.main` is valid again, typically the next frame or two) fires normally. This was almost certainly the "clicking sometimes does nothing, no pattern" bug: an unguarded `Camera.main.ScreenToWorldPoint` threw mid-`Fire()`, aborting before `EffectActive` was ever set false, with no crash log visible to the player and no consistent trigger to notice.

**Full vision while aiming.** `Patches/SniperVisionPatch.cs` postfixes `ShipStatus.CalculateLightRadius` — the same method TOU-Mira's own `VisionPatch` uses for every other role-based vision change — and sets the result to `ShipStatus.MaxLightRadius` while the Sniper's aim window (`EffectActive`) is open. This is the identical value TOU-Mira's `VisionPatch` already grants dead players ("ghosts see the whole map, including through walls"), confirming a single radius value is sufficient — Among Us's vision darkness has no separate wall-occlusion check independent of that radius. Runs at `Priority.Low` so it always resolves after TOU-Mira's own postfix (which has no explicit priority, i.e. `Priority.Normal`) and gets the final say. Purely local and self-correcting: no manual restore is needed on aim-end, since the very next frame's `CalculateLightRadius` call just runs TOU-Mira's postfix unopposed once our condition stops matching.

## Design decisions

- **Bullet invisible to others by default.** Behind `BulletVisibleToOthers` toggle — the Sniper can remain hidden. Only the victims and the Sniper know where the shot came from.
- **Piercing line, not cone.** Uses perpendicular-distance-to-line, not ATR's closest-target cone. Allows the Sniper to pierce clusters.
- **Aim window is timed, not toggle.** Pressing doesn't toggle aim on/off, just opens the window. Time pressure adds risk/reward.
- **Firing always ends aim and starts cooldown.** Regardless of whether any targets were hit. Prevents spam.
- **UI-click guard prevents accidental fires.** Especially important since the Sniper needs to aim at world positions, not click the button again.

## Playtest history

- **2026-07 first playtest: "sniper doesn't work".** Two root causes found and fixed: (1) click polling lived in the button's FixedUpdate and dropped most clicks (see above); (2) hit test was line-vs-center-point with a 0.2u corridor, so even registered shots whiffed. Both reworked — needs a re-test.
- **2026-07-16 second playtest: "sniper works now, but the projectile placeholder should only be visible when they actually fire".** The continuous aim guide (visible for the whole aim window) was removed; see "No aim guide" above.
- **2026-07-16 third playtest: "clicking sometimes does nothing, no pattern", "shot should originate from sprite middle, not feet", "should see through walls while aiming".** All three addressed: `Camera.main` null-guard (see "Camera.main can be transiently null" above), `SniperShots.GetShotOrigin()` (see "Shot origin" above), and `SniperVisionPatch` (see "Full vision while aiming" above). Root-caused via the real game assembly (IL2CPP interop metadata, `ilspycmd`-decompiled) rather than guesswork — confirmed `PlayerControl.cosmetics.nameText` is a plain world-space `TMPro.TextMeshPro` (ruling out a nametag-blocks-EventSystem theory) and confirmed `ShipStatus.CalculateLightRadius`/`MaxLightRadius` as the real vision mechanism (ruling out a separate wall-raycast-occlusion system that would have needed its own bypass).

## Not yet verified in-game / known follow-ups

- Re-test after the click/hit-detection rework (see Playtest history).
- Re-test aiming without the guide sprite: confirm the shot still fires accurately toward the clicked point and the bullet visual only appears on fire.
- Re-test click reliability: confirm clicks no longer silently fail; if they still do occasionally, the `Camera.main == null` guard should at least mean the aim window stays open rather than eating the attempt.
- Re-test full vision: confirm the whole map lights up (including other rooms) for the Sniper's own client only while aiming, and that it reverts immediately when the aim window ends or is cancelled.
- Role icon and ability sprite are placeholder art.
- The UI-layer `Physics2D.OverlapPoint` probe assumes HUD button colliders live on the "UI" layer — confirm no HUD element is missed (clicking the kill button while aiming must not fire).
- Z-depth sorting (`position.y / 1000f - 1f`) should be verified to ensure the bullet renders correctly relative to the environment.
