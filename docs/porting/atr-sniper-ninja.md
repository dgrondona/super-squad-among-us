# AllTheRoles Sniper & Ninja Role Porting Guide

Decompiled from AllTheRoles (ATR), examining roles for re-implementation in another mod.

## Sniper Role

### Behavior Specification

The Sniper is an Impostor-aligned killing role with a long-range ranged attack mechanic.

#### Ability Flow

**Button Name:** "SNIPE" (locale key: `button.label.snipe`)

**Initial Button Action (Toggle Aiming Mode):**
1. Player presses the Snipe button to enter aiming mode
2. Upon activation, a message is displayed: "Use your right mouse button to shoot in the direction of your cursor!" (locale key: `role.sniper.shoot.notify`)
3. A weapon guide sprite appears on the player (rendered at camera z-index -30)
4. A targeting scope/guide is rendered showing the aiming direction based on mouse position

**Aiming Mechanics (ig.jt() method):**
- Aiming is done via mouse direction: angle calculated as `Mathf.Atan2(mouseY, mouseX)` where mouse position is relative to screen center
- The weapon sprite follows the cursor, with transform position interpolating toward cursor direction by 40% each frame
- Sprite rotation reflects the aim angle, with horizontal flip applied when aiming backward (when `Cos(angle) < 0`)
- Player must remain out of vents for the weapon to be visible

**Targeting System (ig.ak() method - overloaded with radius/range parameters):**
- **Shot width (cone):** `gm * 0.2f` where `gm = 1f` (default), so ~0.2 units wide
- **Shot range:** `gn = 20f` (default, configurable)
- **Coordinate transformation:** Converts world positions to sniper's relative coordinate space using Atan2 rotation transform (rotates around the aim direction)
- **Hit detection:** Player/corpse position is inside the shot if:
  - `|rotated_y| < width` (within cone width)
  - `rotated_x >= 0` (in front of sniper)
  - `rotated_x < range` (within max range)
  - Distance-based selection: closest player/corpse in cone is hit (no multi-hit)

**Secondary Fire / Kill (Right Mouse Button while aiming):**
1. When right-clicked during aiming mode, performs the actual shot
2. Raycast/targeting is re-run using `e3` class to find target
3. If valid target found:
   - Calls RPC `ak()` (MethodRpc ID 114) with target player ID
   - Resets aiming state locally
4. Aiming mode is exited regardless of hit or miss
5. Kill is synced via network RPC

**Special Targeting Considerations:**
- Can target both PlayerControl and DeadBody (corpses via Joker role)
- If "Can Kill Impostors" option is OFF (`fy = false`), Impostors are filtered out from valid targets
- Checks player shield status via base game protections (Guardian Angel, etc.) via `n.a()` kill validation

**Visual Feedback:**
- **Weapon rendering:** SpriteRenderer on a "Weapon" GameObject, child of the sniper player's transform
  - Sprite: "SniperGuide.png" (asset from bundle via `f9.bs()`)
  - Color: Player's color palette
  - Position: Offset by weapon direction at ~0.4 units distance
- **Shot firing effect:** On kill confirmation, a line/arrow effect (`ag` class) appears showing the shot trajectory
  - Color: Red (`Color.red`)
  - Duration: 10 frames (~0.167s in Unity timeline)
  - Sprite: "Arrow.png" (from `f9.bw()`)
  - Fade timing: color changes as per `ig.jt()` closure lambda (lines 36-43)

**Kill Message:**
- Broadcast message: "Shots Fired!" (locale key: `role.sniper.message`)
- Visible only to the sniper (not AmOwner condition at line 537 in RPC handler)
- Shown as a floating text notification in red color at screen position (0, 1, -20)
- Duration: 2 seconds

**Meeting / Death Handling:**
- Aiming state is cleared on meeting start via `au()` method (line 199)
- Aiming state is cleared on death via `bc()`, `bd()`, `b5()` methods
- All targeting circles are removed from the tracking dictionary `gu`
- Weapon GameObject is deactivated

### Options

| Option Key | Locale Key | Default | Min | Max | Step | Type |
|------------|-----------|---------|-----|-----|------|------|
| Spawn Chance | `option.impostor.sniper.name` | 50 | 0 | 100 | 10 | SpawnChance |
| Kill Cooldown | `option.impostor.sniper.snipercd` | 25s | 10s | 40s | 2.5s | FloatOption |
| Can Kill Impostors | `option.impostor.sniper.cankillimps` | false | N/A | N/A | N/A | BoolOption |
| Can Vent | `option.impostor.sniper.vent` | false | N/A | N/A | N/A | BoolOption |

**Option Registration Code:** See `ig.av()` method (lines 502-514). Uses `a7.a()` static factory methods with numeric IDs 30200-30203.

### Networking

**RPC Method:** `ig.ak(PlayerControl A_0, byte A_1)` at `[MethodRpc(114u)]` (line 516)

**RPC Payload:**
- `A_0`: Sniper player
- `A_1`: Target player ID (byte)

**Sync Behavior:**
- Called when the local sniper player confirms a shot
- Target info is serialized as player ID only; no position/trajectory data is sent
- All clients resolve the kill locally using the same targeting logic
- Kill is processed via `n.a(playerControl, targetPlayerId, d9.b, ea.c, false, true)` on the sniper's client (line 232)

**Visibility:**
- Sniper kill indicator message ("Shots Fired!") visible only to the killer (AmOwner check on line 537)
- Other players see standard kill animation

### Edge Cases

1. **Meeting During Aiming:** Aiming state is cleared; weapon sprite hidden
2. **Death During Aiming:** All rendering cleaned up; corpses no longer tracked
3. **Target Dies Before Shot Lands:** RPC still fires but kill validation in `n.a()` checks target liveness
4. **Target Disconnects:** Similar to death - kill validation should fail
5. **Venting While Aiming:** Weapon sprite automatically hides (not visible in vent)
6. **Sabotages (Comms, Lights):** No special handling detected; targeting continues normally
7. **Shields/Protections:** Validated through standard `n.a()` kill check; Sniper respects shield protections
8. **Camera Visibility:** No restrictions on target visibility through walls or cameras (pure angle-based)
9. **Admin Table:** Sniper appears as normal Impostor; no special icon

### Assets

| Asset Type | Asset Name | Method | Purpose |
|------------|-----------|--------|---------|
| Sprite | SniperButton.png | `f9.am()` | Kill button icon |
| Sprite | SniperGuide.png | `f9.bs()` | Weapon/aiming sight visual |
| Sprite | Arrow.png | `f9.bw()` / `f9.cr()` | Shot trajectory effect |

---

## Ninja Role

### Behavior Specification

The Ninja is an Impostor-aligned role with a multi-step mark-and-assassinate mechanic, featuring invisibility and kill-location tracking.

#### Ability Flow

**Button Name:** "MARK" (locale key: `button.label.mark`)

**State 1: Targeting (Mark Phase)**
1. Player presses the MARK button to target a player
2. Button targeting uses collision/targeting system to identify nearby players (method `n.a()` with `A_0.ao` field)
3. On successful target selection:
   - Target player is stored in `ia.ga` field
   - Kill arrow is created via `ah.a()` pointing to target location
   - Button visuals change: main button is hidden, secondary button "ASSASSINATE" becomes available
   - A trace/trail effect is created at the target's current location (via `c7` class)
   - Kill arrow tracking begins (repeats every ~5 seconds based on `fx` duration value)
4. Arrow tracking is location-based via lambda: `() => dx.bx(a10.ga) ? ((Vector3?)null) : new Vector3?(((Component)a10.ga).transform.position)` (line 102)
5. If "Ninja Knows Location Of Target" option is true (`fw = true`), arrow points to exact target location

**State 2: Invisible (Assassination Phase)**
1. Once target is marked, pressing the button again (now labeled "ASSASSINATE") triggers the kill
2. Ninja becomes **invisible** upon assassination
3. Invisibility properties:
   - **Duration:** `f5` (default 10 seconds, range 5-40s) - stored in `ia.f9`
   - **Visual state:** Ninja's player outfit is changed to a low-alpha state via `v.b(clear_color)` where alpha = 0.1f for crew, 0.0f for dead (line 180)
   - **Who sees invisibility:** Only if the local player is NOT the ninja and NOT dead (line 176)
   - **Other players' perspective:** Ninja appears fully transparent to non-owners, slightly visible to crew
4. Invisibility countdown: `f9 -= Time.deltaTime` (line 170)
5. If Ninja dies during invisibility: `f9 = 0f` (line 173), visibility restored immediately

**Trace/Trail Effect (c7 class):**
- Created at both Ninja position and target position when kill occurs
- Visual: Sprite "Caltrops.png" (`f9.cd()`) at position marked with player color
- Duration: `fx` parameter (default 5 seconds, range 1-20s, step 0.5s)
- **Color fade:**
  - First phase (0 to `fx`): Lerp from player color to white over duration via `a7.b(fx)` 
  - Second phase (0.9 to 1.0): Fade out alpha over final 10% of duration
  - Fade rate controlled by `fy` (default 2s, range 0-20s) - controls how long color stays visible before white flash

**Kill Arrow (Astral Trail):**
- Created via `ah.a()` with color `gr.Impostor` (red/pink)
- Points to target location when target is alive
- Arrow sprite: "Arrow.png" (from `f9.cr()`)
- Arrow position updates: Every `fx` duration (default 5s) if target still alive
- Arrow vanishes if target dies or becomes unreachable

**RPC Call:**
- Kill is synced via `ia.ak(PlayerControl A_0, PlayerControl A_1)` at `[MethodRpc(99u)]` (line 210)
- Called on the Ninja's client only (AmOwner check line 230)

### Options

| Option Key | Locale Key | Default | Min | Max | Step | Type |
|------------|-----------|---------|-----|-----|------|------|
| Spawn Chance | `option.impostor.ninja.name` | 50 | 0 | 100 | 10 | SpawnChance |
| Kill Cooldown | `option.impostor.ninja.killcd` | 25s | 10s | 40s | 2.5s | FloatOption |
| Ninja Knows Location | `option.impostor.ninja.knowslocation` | true | N/A | N/A | N/A | BoolOption |
| Trace Duration | `option.impostor.ninja.traceduration` | 5s | 1s | 20s | 0.5s | FloatOption |
| Invisible Duration | `option.impostor.ninja.invisduration` | 10s | 5s | 40s | 2.5s | FloatOption |
| Trace Color Fade | `option.impostor.ninja.tracecolorfade` | 2s | 0s | 20s | 0.5s | FloatOption |
| Can Vent | `option.impostor.ninja.vent` | false | N/A | N/A | N/A | BoolOption |

**Option Registration Code:** See `ia.av()` method (lines 190-208). Uses `a7.a()` with numeric IDs 30600-30606.

### Networking

**RPC Method:** `ia.ak(PlayerControl A_0, PlayerControl A_1)` at `[MethodRpc(99u)]` (line 210)

**RPC Payload:**
- `A_0`: Ninja player
- `A_1`: Target player

**Sync Behavior:**
1. Sets Ninja's invisibility state: `ia2.f9 = global::a7.b(f5)` (line 227)
2. Calls `ia2.ju()` to apply invisibility rendering (line 228)
3. Creates traces at both Ninja position and target position: `new c7(A_0, Vector2.op_Implicit(position), global::a7.b(fx))` (lines 229, 238)
4. If AmOwner (line 230): Calls actual kill via `n.a(A_0, A_1, d9.b, ea.c, false, true)` (line 232)
5. Checks waterline/platform height for animation via `cx.g()` (line 234)
6. Creates second trace at target position (line 238)

**Invisibility Application (v.b() method):**
- Sets player outfit to state `ee.c` (invisibility state enum)
- Applies very low alpha (0.1f to crew, near-zero to dead)
- Hides player name/color-blind name text via `((Graphic)A_0.cosmetics.nameText).color = Color.clear` (line 180)

### Edge Cases

1. **Target Dies Before Assassination:** Arrow visually disappears but button state may persist; edge case handling via `dx.bx(ga)` checks throughout
2. **Ninja Dies During Invisibility:** Invisibility immediately cleared in `bc()`, `bd()`, `b5()` death handlers (lines 122, 129, 136)
3. **Target Disconnects:** Arrow returns null from location lambda, arrow hides
4. **Mark Cleared by Meeting:** `ga = null` in `au()` (lines 151-155); button states reset
5. **Target Dead During Mark:** Next assassination clears mark but may fail kill check
6. **Multiple Rapid Marks:** Previous target's arrow is cleaned up via `ah.b(this)` (line 257)
7. **Invisibility + Venting:** No special interaction; venting breaks invisibility normally (standard game mechanic)
8. **Invisibility + Meeting:** Invisibility remains active if meeting ends with Ninja still in state (persists until expiry or death)
9. **Camera Visibility:** Invisible Ninja does not appear on cameras per `v` class visibility logic (gray player state with opacity)
10. **Admin Table:** Invisible Ninja shown as gray player (state `ee.c`) per visibility system

### Assets

| Asset Type | Asset Name | Method | Purpose |
|------------|-----------|---------|---------|
| Sprite | MarkButton.png | `f9.u()` | Mark/targeting button icon |
| Sprite | DraftRandomIcon.png | `f9.d()` | Assassinate button icon (generic placeholder) |
| Sprite | Arrow.png | `f9.cr()` | Kill arrow/tracking indicator |
| Sprite | Caltrops.png | `f9.cd()` | Trace/trail visual at kill location |

---

## Mangled File Map

Complete reference of decompiled .cs files corresponding to role components and supporting infrastructure.

### Primary Role Classes

| File | Class | Purpose |
|------|-------|---------|
| `/reference/AllTheRoles-decompiled/ig.cs` | `ig` (class decorated with `[jx(RoleEnum.Sniper, ...)]`) | **Sniper role main class.** Contains: ability flow (`jt()`), aiming/targeting (`ak()` overloads), option registration (`av()`), RPC handler for kills (`ak(PlayerControl, byte)`). Fields: `gm` (shot width), `gn` (range), `go` (kill effect range), `gp` (fade-in time), `gq` (fade duration), `gu` (tracking circle dict), `gv`/`gw` (weapon GameObject/sprite). |
| `/reference/AllTheRoles-decompiled/ia.cs` | `ia` (class decorated with `[jx(RoleEnum.Ninja, ...)]`) | **Ninja role main class.** Contains: mark/assassinate flow (nested `am()` method), invisibility state (`jx()`, `ju()`), option registration (`av()`), RPC handler (`ak(PlayerControl, PlayerControl)`). Fields: `f8` (invisibility active flag), `f9` (invisibility remaining time), `ga` (target player ref), `gb` (button). |

### Option System

| File | Class | Purpose |
|------|-------|---------|
| `/reference/AllTheRoles-decompiled/a7.cs` | `a7` | **Base option class.** Manages option values, serialization, UI display. Static factory methods `a(...)` create typed options (float, bool, string). Handles registration with game options, config file persistence, RPC sync. Used by `ig.av()` and `ia.av()` for option creation. |
| `/reference/AllTheRoles-decompiled/bb.cs` (assumed) | `bb` | Float option variant; created by `a7.a(int, a6, Color, string, float, float, float, float, ...)` |
| `/reference/AllTheRoles-decompiled/ba.cs` (assumed) | `ba` | Boolean option variant; created by `a7.a(int, a6, Color, string, bool, ...)` |

### Targeting & Hit Detection

| File | Class | Purpose |
|------|-------|---------|
| `/reference/AllTheRoles-decompiled/e3.cs` | `e3` | **Targeting result container.** Holds sniper's raycast result: PlayerControl `b` (target), float `c` (distance to player), db `d` (fake/corpse), float `e` (distance to corpse). Methods `m()`/`j()`/`k()` check validity and retrieve closest target. Static `j(e3, e3)` equality check. |
| `/reference/AllTheRoles-decompiled/n.cs` | `n` | **Game interaction utilities.** Static methods for kills, corpse queries, vent access, targeting. Methods used: `n.a(PlayerControl, e3, d9, ea, bool, bool)` performs kill with targeting validation; `n.b(byte)` / `n.a(byte)` retrieve player by ID; `n.c(PlayerControl, PlayerControl)` checks kill legality (returns `eh` enum). |

### Rendering & Visual Effects

| File | Class | Purpose |
|------|-------|---------|
| `/reference/AllTheRoles-decompiled/at.cs` | `at` | **Targeting guide line renderer.** Creates SpriteRenderer GameObject for aiming guides. Methods: `a(Sprite)` set sprite, `a(Color)` set color, `a(Color, Vector3)` update position and rotate toward target. Used for Sniper's aiming circles visualization. |
| `/reference/AllTheRoles-decompiled/ag.cs` | `ag` | **Arrow/navigation indicator renderer.** Uses ArrowBehaviour component for arrow pointing. Methods: `a(Vector3, Color?)` update position/color. Used for Ninja's kill arrow (`ah` system manages updates). |
| `/reference/AllTheRoles-decompiled/c7.cs` | `c7` | **Trace/trail sprite pool.** Creates a SpriteRenderer at a position showing player traces. Animates color fade over time via `Effects.Lerp()` coroutines. Static list `b` tracks all traces for cleanup. Used by Ninja RPC handler to create traces at kill location. |
| `/reference/AllTheRoles-decompiled/ah.cs` | `ah` | **Arrow management system.** Static class managing multiple kill arrows (ConcurrentDictionary keyed by role owner and target). Methods: `a(object, PlayerControl, object, Func<Vector3?>, Color, float, bool)` create/update arrow, `b(object, object)` remove arrow. Updates arrow position via lambda function passed in. Used by Ninja to maintain kill arrow pointing to target. |

### Visibility & Cosmetics

| File | Class | Purpose |
|------|-------|---------|
| `/reference/AllTheRoles-decompiled/v.cs` | `v` | **Visibility/invisibility system.** Static extension methods for PlayerControl. Method `b(PlayerControl, Color)` sets player to invisibility state with color tint; `b(PlayerControl)` restores visibility. Changes outfit via `a(ee)` state enum. Hides name text. Used by Ninja RPC to apply invisibility. |

### Networking & RPC

| File | Class | Purpose |
|------|-------|---------|
| N/A | `[MethodRpc(...)]` | Reactor attribute on static methods in `ig` and `ia` marking them as RPC handlers. ID 114 = Sniper kill RPC, ID 99 = Ninja kill RPC. |

### Enums & Constants

| File | Enum/Class | Values/Purpose |
|------|-----------|---------|
| `/reference/AllTheRoles-decompiled/AllTheRoles/Modules/Data/RoleEnum.cs` | `RoleEnum` | Line 44: `Ninja`, Line 49: `Sniper`. Used to identify roles throughout codebase. |
| `/reference/AllTheRoles-decompiled/d9.cs` | `d9` | Kill type enum: `a`, `b`, `c`, `d`. Passed to `n.a()` kill function; semantic meaning not decompiled but likely: normal kill, suicide, trap, etc. Both Sniper and Ninja use `d9.b`. |
| `/reference/AllTheRoles-decompiled/ea.cs` | `ea` | Kill effect/result enum: `a`, `b`, `c`, `d`, `e`, `f`. Determines how kill is handled by game systems. Both Sniper and Ninja use `ea.c`. |
| `/reference/AllTheRoles-decompiled/eh.cs` (inferred) | `eh` | Kill legality enum. Values like `eh.b`, `eh.c` returned by `n.c()` indicating if kill is legal, protected, or invalid. |
| `/reference/AllTheRoles-decompiled/ee.cs` (inferred) | `ee` | Player outfit/state enum. Values like `ee.a` (normal), `ee.c` (invisibility), `ee.d` (dead state), `ee.e` (ghost state). Used by `v` class. |

### Asset Loading

| File | Class | Purpose |
|------|-------|---------|
| `/reference/AllTheRoles-decompiled/f9.cs` | `f9` | **Asset bundle loader.** Static field `a` loads "atr" bundle. Static methods return cached sprites: `am()` = SniperButton, `bs()` = SniperGuide, `bw()`/`cr()` = Arrow, `u()` = MarkButton, `d()` = placeholder assassinate button, `cd()` = Caltrops trace. All use lazy-loaded `ga<Sprite>` / `gb<Sprite>` wrapper pattern. |

### Locale Strings

| Locale Key | English Value | Used By | Context |
|-----------|--------------|---------|---------|
| `role.sniper.name` | "Sniper" | Role display | |
| `role.sniper.description` | "Kill players from far away!" | Role info | |
| `role.sniper.message` | "Shots Fired!" | Sniper RPC | Kill announcement |
| `button.label.snipe` | "SNIPE" | `a2.a()` button creation | Sniper ability button |
| `option.impostor.sniper.name` | "Sniper" | Option spawn chance | |
| `option.impostor.sniper.snipercd` | "Snipe Cooldown" | Option display | Cooldown setting |
| `option.impostor.sniper.cankillimps` | "Sniper Can Kill Impostors" | Option display | Friendly fire toggle |
| `option.impostor.sniper.vent` | "Can Vent" | Option display | Vent permission |
| `role.ninja.name` | "Ninja" | Role display | |
| `role.ninja.description` | "Surprise and assassinate your foes" | Role info | |
| `button.label.mark` | "MARK" | `a2.a()` button creation | Ninja marking button |
| `button.label.assassinate` | "ASSASSINATE" | Button state change | Secondary button after mark |
| `option.impostor.ninja.killcd` | "Kill Cooldown" | Option display | Cooldown setting |
| `option.impostor.ninja.knowslocation` | "Ninja Knows Location Of Target" | Option display | Arrow tracking behavior |
| `option.impostor.ninja.traceduration` | "Trace Duration" | Option display | Trail visual lifetime |
| `option.impostor.ninja.invisduration` | "Invisible Duration" | Option display | Invisibility duration |
| `option.impostor.ninja.tracecolorfade` | "Time Till Trace Color Has Faded" | Option display | Color fade timing |
| `option.impostor.ninja.vent` | "Can Vent" | Option display | Vent permission |

### File Organization Note

All decompiled files are in `/reference/AllTheRoles-decompiled/` with obfuscated filenames. The role classes `ig` and `ia` are located at the root level (not in subdirectories). Supporting classes are scattered throughout based on Dotfuscator obfuscation; their real names were recovered from string literals and reflection inspection during decompilation.

---

## Notes for Re-Implementation

### Known Unknowns

1. **Sniper Shot Sound:** No audio clip reference found in `ig.cs`. May be handled by standard kill sound or external SFX system.
2. **Ninja Assassination Animation:** While invisibility rendering is clear, the assassination animation blend/transition is not explicit in decompiled code; may use standard kill animation with override.
3. **d9 & ea Enum Semantics:** The exact behavioral difference between `d9.a/b/c/d` and `ea.a/b/c/d/e/f` is not apparent from enum definitions alone; empirical testing or additional code context needed.
4. **Assassinate Button Icon:** Uses generic `DraftRandomIcon.png` placeholder; proper icon asset name should be confirmed from actual ATR asset bundle.

### Implementation Priorities

1. **Sniper shot width calculation** relies on cosine/sine coordinate transforms; exact cone width math at lines 468-469 of ig.cs is critical for accuracy.
2. **Ninja invisibility timing** is tied to frame-by-frame `Time.deltaTime` countdown; ensure delta time integration matches Among Us frame rate assumptions.
3. **Option default values** must match exactly (25s cooldown, 10s invisibility, etc.) for gameplay balance.
4. **RPC method IDs** (114 for Sniper, 99 for Ninja) must not collide with other mods' RPC handlers if adapting to a different framework.
5. **Trace color fade** has two simultaneous lerps in `c7.c()` method—one for color shift, one for alpha fade—timing must be synchronized.

---

## References & Tools Used

- **Decompile Tool:** ILSpy (or similar IL2CPP .NET decompiler)
- **Obfuscation Decoder:** String literal search + manual class correlation
- **Source Location:** `/home/draven/super-squad-among-us/reference/AllTheRoles-decompiled/`
- **Locale Data:** `AllTheRoles.Resources.Languages.Lang.dat` (UTF-8 key-value pairs)
- **Asset Bundle:** "atr" loaded via `AssetBundleManager.Load("atr")` in `f9.cs`
