# Sniper

Impostor Killing role. Press the secondary ability button to shoulder the rifle and enter aiming mode — no aim assist is shown while deciding where to click; the projectile visual only appears at the moment you fire. Click anywhere in the world to fire a piercing bullet that goes through walls and kills everyone within 0.2 units of the bullet's path. Aiming lasts for a configurable window (default 10s); if you don't fire, the aim times out and the cooldown begins. The bullet can optionally be invisible to other players (configurable).

Files: `Roles/Impostor/SniperRole.cs`, `Buttons/Impostor/SniperSnipeButton.cs`, `Modules/SniperShots.cs`, `Options/Roles/Impostor/SniperOptions.cs`.

Design: adapted from AllTheRoles per the user's own spec; see `docs/porting/README.md`.

## How it works

**Aim-window pattern.** Press the button to shoulder and enter aiming mode; stays armed until you fire or the aim window expires. No re-click needed, just click in the world to fire. Fires on left-click input (not the HUD button again).

**Aiming and firing run per rendered frame, not on the button's FixedUpdate.** `Patches/SniperAimPatch.cs` (a `HudManager.Update` postfix) calls `SniperSnipeButton.HandleAimFrame()` every frame. This matters: MiraAPI button `FixedUpdate` runs on `PlayerControl.FixedUpdate`'s fixed tick, and `Input.GetMouseButtonDown` is only true during the one rendered frame of the press — polling it from the fixed tick drops most clicks at any frame rate above the tick rate. This was the primary reason the first playtest reported "sniper doesn't work". Same pitfall and same fix as the Apparater's map click (see docs/roles/apparater.md).

**Hit detection is line-vs-hitbox, not line-vs-point.** `SniperShots.FindHits` treats each player as a circle around their **body sprite bounds** (center + max extent, ~0.5u; collider-position fallback) and hits when perpendicular distance to the shot line ≤ 0.2 (bullet half-width) + that radius. The original line-vs-`GetTruePosition()` test whiffed unless the line passed within 0.2u of the feet — the second playtest bug. Pierces clusters (all hits, ordered by distance). Skips: the Sniper, dead/disconnected, players in vents, `FirstDeadShield` holders, players with a `DisabledModifier` that has `CanBeInteractedWith == false` (devoured, ambush-hidden, ...), and — if configured — impostor-aligned players (`IsImpostorAligned()`, which covers alliance modifiers too).

**Shot origin is the shooter's sprite center, not their feet.** `PlayerControl.GetTruePosition()` sits at the collider/feet position, well below the visible body sprite — a line drawn from there looked like it started at the ground instead of the gun/chest. `SniperShots.GetShotOrigin()` reuses the same body-bounds lookup `FindHits` already uses for victims (`GetBodyCircle(player).Center`), so `Fire()`'s hit-test line and the bullet's visual start point both originate from the sprite's actual center.

**No aim guide.** Earlier versions rendered a persistent local arrow pointing from the Sniper's body toward the cursor for the whole aim window, using the same sprite as the bullet travel visual (`SniperGuideSprite`). Removed per playtest feedback (2026-07-16): the projectile placeholder should only be visible when the Sniper actually fires, not while still deciding where to aim. Direction is now computed purely from the cursor position at the instant of the click in `Fire()` — no visual feedback during the aim window itself.

**Frozen while aiming.** Pressing the button roots the Sniper in place: `BeginAim()` sets `moveable = false`, calls `MyPhysics.ResetMoveState()`, and sets `NetTransform.SetPaused(true)` — the same freeze `DevouredModifier` uses (minus the network sync concerns, since this affects only the local player). The freeze is re-asserted every rendered frame in `HandleAimFrame()` (`moveable = false` check) to counteract vanilla animations that flip it back on. Released in idempotent `EndAim()` (guarded by `aimLockActive`) whenever the aim window ends — firing, timeout, or death/meeting cancel. Side effect: freezing makes the vanilla `CanMove` false, which blocks the vanilla kill button while aiming. `Enabled` is overridden to stay true while `EffectActive`/`aimLockActive` so the death-cancel path still runs if the Sniper dies mid-aim (see the "buttons stop ticking on death" entry in docs/il2cpp-gotchas.md).

**Wall vision while aiming.** While `EffectActive` is true, `HandleAimFrame()` disables `HudManager.Instance.ShadowQuad.gameObject` (the game's sole wall-occlusion mechanism). The shadow quad is the actual barrier that prevents seeing through walls; the light radius only sizes the darkness circle. Pattern copied from TOU-Mira: `reference/TOU-Mira/TownOfUs/Modules/MedSpirit/MedSpiritObject.cs:129/177`, `Roles/Other/SpectatorRole.cs`, and `Patches/HudManagerPatches.cs:99` (vanilla's restore rule: `SetActive(!PlayerControl.LocalPlayer.Data.IsDead)`). The disable is re-asserted every rendered frame per "self-heal doctrine" (since other mods' patches might touch it on their own schedule), and `EndAim()` restores with exactly the vanilla rule: on for the living, off for the dead.

**Bullet visual removed entirely.** User decision 2026-07-16: the Sniper renders nothing when firing — no aim guide during the window, no bullet travel on fire. The `BulletVisibleToOthers` option was deleted from `SniperOptions.cs` (and its locale line). `SniperShots.RpcShowShot`/`ShowShotLocally` are KEPT as reusable machinery for future roles that copy the shot logic but want a visible projectile (see the updated header comment in `SuperSquadAmongUs/Modules/SniperShots.cs`).

**Click-miss hardening round 2.** The `Camera.main` guard alone did not fix intermittent misses. Three complementary changes: (1) `Patches/SniperAimPatch` runs at `HarmonyPriority(Priority.First)` and wraps `HandleAimFrame()` in try/catch — when any earlier postfix on the shared `HudManager.Update` method throws, Harmony aborts the remaining postfixes for that frame, and TOU-Mira has several patches on that method; being skipped on the click's frame silently ate the shot (see docs/il2cpp-gotchas.md entry "One throwing Harmony postfix skips the rest of the chain"). (2) `IsClickOnHud()`'s UI-layer `Physics2D.OverlapPoint` probe now only blocks when the hit collider's parent is a `PassiveButton` — `HudManager` is parented to the camera so UI-layer objects physically overlap the play area in world space, and non-interactive layer-5 objects (e.g. TOU's tracking arrows) could match ANY collider and block legitimate clicks. (3) EVERY click-rejection path now logs `Info("Sniper: click ignored - <reason>")` to the BepInEx log (`BepInEx/LogOutput.log`) — the next "click did nothing" report is diagnosable from the log instead of guessed at.

**UI-click guard, refined.** The frame the button was armed is recorded (`armedFrame`) so the arming click itself can never fire the shot. Firing is also blocked while hacked/disabled (`GlitchHackedModifier`/`DisabledModifier`), while the map is open, and while the chat is open. The disabled check must use `GetModifiers<DisabledModifier>().Any(x => !x.CanUseAbilities)`, not a bare `HasModifier<DisabledModifier>()` — some subclasses (`GrenadierFlashModifier`, `EclipsalBlindModifier`) set `CanUseAbilities = true` to explicitly opt out of blocking abilities, matching the gating `TownOfUsButton.CanUse()` already applies to the arming click.

## Design decisions

- **The shot renders nothing, for anyone.** No aim guide, no projectile (user decision 2026-07-16) — the kill itself is the only tell. The visual machinery survives in `SniperShots` for future roles.
- **Piercing line, not cone.** Uses perpendicular-distance-to-line, not ATR's closest-target cone. Allows the Sniper to pierce clusters.
- **Aim window is timed, not toggle.** Pressing doesn't toggle aim on/off, just opens the window. Time pressure adds risk/reward.
- **Firing always ends aim and starts cooldown.** Regardless of whether any targets were hit. Prevents spam.
- **UI-click guard prevents accidental fires.** Especially important since the Sniper needs to aim at world positions, not click the button again.

## Playtest history

- **2026-07 first playtest: "sniper doesn't work".** Two root causes found and fixed: (1) click polling lived in the button's FixedUpdate and dropped most clicks (see above); (2) hit test was line-vs-center-point with a 0.2u corridor, so even registered shots whiffed. Both reworked — needs a re-test.
- **2026-07-16 second playtest: "sniper works now, but the projectile placeholder should only be visible when they actually fire".** The continuous aim guide (visible for the whole aim window) was removed; see "Bullet visual removed entirely" above.
- **2026-07-16 third playtest: "clicks still sometimes not registering; wall vision not working".** Root-caused as a Harmony postfix-chain abort: when an earlier postfix on the shared `HudManager.Update` method throws, Harmony skips all remaining postfixes for that frame, silently eating the click. Fixed by running at `Priority.First` + exception-guarding the handler (see "Click-miss hardening round 2" above), plus logging every rejection to the BepInEx log for diagnostics. Wall-occlusion fix: found via TOU-Mira source that `HudManager.ShadowQuad` is the sole wall-occlusion mechanism (not the light radius, which only sizes the darkness circle); now disabled while aiming and restored post-fire/timeout/cancel/death (see "Wall vision while aiming" above). All three axes now addressed: frozen-in-place while aiming (Ambusher-style freeze, re-asserted per frame, released on all three exits); wall vision working (shadows disabled/restored correctly); click-reliability logging in place.

## Re-test checklist

- **Clicks register reliably.** Click anywhere in the aim window and expect the shot to fire. If a click is eaten: grab the `Sniper:` lines from `BepInEx/LogOutput.log` to identify the rejection reason.
- **Wall vision works.** Can see the whole ship (including other rooms) through walls while aiming. Shadows restore completely after firing, timeout, cancelling due to death/meeting, or any exit from the aim state.
- **Frozen while aiming.** Cannot move during the aim window. Movement restores on fire, timeout, death, or meeting—all three exit paths.
- **No projectile or guide appears.** No aim assist line during the window; no bullet travel visual on fire. (The rendering machinery is preserved in `SniperShots` for future roles.)
- Role icon and the Snipe button sprite have no dedicated art yet. The role icon uses Town of Us:
  Mira's generic Impostor team icon (`Resources/Placeholders/Impostor.png`); the Snipe button
  temporarily reuses TOU-Mira's Time Lord Rewind sprite (`TouCrewAssets.RewindSprite`, same one the
  Astral's Phase button borrows) - the team icon was too large to read as a HUD button. Swap out
  when real art exists.
- Z-depth sorting should be verified if the bullet ever gets a visual again.
