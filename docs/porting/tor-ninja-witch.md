# TheOtherRoles Ninja and Witch Role Documentation

## Ninja Role

### Role State
**Files and Locations:**
- State: `TheOtherRoles/TheOtherRoles.cs:1641-1682` (static class `Ninja`)

**Fields:**
- `ninja`: `PlayerControl` - the ninja player instance (null when not assigned)
- `ninjaMarked`: `PlayerControl` - the currently marked target (null if no target marked)
- `currentTarget`: `PlayerControl` - the closest targetable player within kill distance (refreshed each frame)
- `color`: `Palette.ImpostorRed` - ninja team color
- `cooldown`: `float` - mark cooldown in seconds (default 30f)
- `traceTime`: `float` - how long the trace remains visible after assassination (default 5f)
- `knowsTargetLocation`: `bool` - whether ninja can see arrow pointing to marked target (default true)
- `invisibleDuration`: `float` - how long ninja stays invisible after assassination (default 3f)
- `invisibleTimer`: `float` - countdown timer for invisibility (decremented each frame)
- `isInvisble`: `bool` - flag for whether ninja is currently invisible (spelling note: typo in source)
- `arrow`: `Arrow` - arrow object that points to marked target when `knowsTargetLocation` is true

**Lifecycle:**
- `clearAndReload()` called at: role reset, game start (initializes to defaults from `CustomOptionHolder`)
  - Destroys old arrow if exists, creates new black Arrow with `SetActive(false)`
  - Resets `invisibleTimer` to 0, `isInvisble` to false
  - Line 1678-1680: `TheOtherRoles.cs`

### Full Ability Flow

#### Button Targeting Rules (Lines 860-861, PlayerControlPatch.cs)
- `Ninja.currentTarget = setTarget(onlyCrewmates: Spy.spy == null || !Spy.impostorsCanKillAnyone, ...)`
- By default targets only crewmates (impostor cannot target impostor, unless Spy.impostorsCanKillAnyone enabled)
- Targets closest player within KillDistance range with no obstacles between
- Cannot target: self, dead players, disconnected players, players in vents, or untargetable players (e.g., shielded by Medic)
- Each frame updates target highlight outline in ninja's color

#### Mark Phase (Lines 1708-1710, Buttons.cs)
1. **Trigger condition:** player presses button and `Ninja.currentTarget != null`
2. **Actions:**
   - Set `Ninja.ninjaMarked = Ninja.currentTarget`
   - Set button timer to 5 seconds (quick visual feedback before assassination window)
   - Play sound "warlockCurse" (reuses Warlock mark sound)
   - Send `CustomRPC.ShareGhostInfo` with `GhostInfoTypes.NinjaMarked` to sync marked target to all clients (Lines 1713-1717)
   - Line 1710: `ninjaButton.Timer = 5f;` — marked state persists for up to 5s or until assassination/meeting

#### Assassination Phase (Lines 1655-1696, Buttons.cs)
**Trigger:** player presses button again when `Ninja.ninjaMarked != null`

**Sequence:**
1. **Murder attempt check** (Line 1657):
   - `checkMuderAttempt(Ninja.ninja, Ninja.ninjaMarked)` evaluates:
     - Medic shield blocks kill → `SuppressKill`
     - Mini not grown up blocks kill → `SuppressKill`
     - TimeMaster shield blocks kill → `SuppressKill`
     - Armored modifier blocks kill → `BlankKill`
     - Transportation/vent blocks kill → `SuppressKill`
     - Thief crew-only kill fails → `SuppressKill` (thief dies)
     - Otherwise → `PerformKill`

2. **If `PerformKill`** (Lines 1659-1696):
   - **Place starting trace** at ninja's current position (Lines 1660-1668):
     - Send `CustomRPC.PlaceNinjaTrace` with position as float[2] buffer
     - Creates `NinjaTrace` object at ninja's location (see NinjaTrace section)
   
   - **Apply invisibility** (Lines 1670-1674):
     - Send `CustomRPC.SetInvisible` with playerId and byte.MinValue (0) flag
     - Calls `setInvisible(Ninja.ninja.PlayerId, 0)`:
       - Sets name/hat/pet/skin to empty strings → invisible on game view
       - Sets body alpha to 0 (or 0.1f for self and dead viewers if impostor/dead)
       - Disables colorblind text
       - Sets `Ninja.invisibleTimer = Ninja.invisibleDuration`
       - Sets `Ninja.isInvisble = true`
   
   - **Perform murder** (Lines 1677-1685):
     - Send `CustomRPC.UncheckedMurderPlayer` with flag byte.MaxValue (255):
       - Teleports ninja to target's location
       - Kills target (shows animation)
       - Line 1682-1683: Submerged floor change on kill
   
   - **Place ending trace** at target's original position (Lines 1687-1696):
     - Send `CustomRPC.PlaceNinjaTrace` with target's position

3. **Cooldown handling** (Lines 1699-1705):
   - If `PerformKill` or `BlankKill`: reset button timer to max, set ninja's `killTimer` to vanilla kill cooldown (usually 25s)
   - If `SuppressKill` (kill blocked): reset button timer to 0 (no cooldown)
   - Clear `Ninja.ninjaMarked = null` to reset state

#### Arrow Tracking (PlayerControlPatch.cs:373-398, ninjaUpdate())
**Update every frame when ninja is alive and in-game:**
- Only active if: Ninja owns arrow AND `knowsTargetLocation` is true AND ninja is local player AND not dead
- When `ninjaMarked != null`:
  - Get marked target's position (or dead body position if target died)
  - Update arrow direction/position to point toward target
  - Arrow visible only if target not dead (`trackedOnMap` flag)
- When `ninjaMarked == null` or arrow disabled: arrow sprite inactive

**Arrow rendering:**
- Uses `Arrow` class (Objects/Arrow.cs:4-78)
- Has `ArrowBehaviour` component (unity-provided or custom) that updates rotation/position to face target
- Color: black (set at initialization)
- Sprite: `TheOtherRoles.Resources.Arrow.png` at 200f pixels per unit
- Layer: 5 (UI layer)
- Does NOT appear on camera/admin feeds (arrow is client-side UI object)

#### Invisibility Duration End (PlayerControlPatch.cs:365-371, ninjaUpdate())
**When invisibility timer expires:**
- Trigger: `Ninja.isInvisble && Ninja.invisibleTimer <= 0 && Ninja.ninja == PlayerControl.LocalPlayer`
- Action: Send `CustomRPC.SetInvisible` with byte.MaxValue flag (255)
- Calls `setInvisible(..., byte.MaxValue)` which:
  - Restores body color to white
  - Restores name/hat/pet/skin from player's default outfit
  - Re-enables colorblind text
  - Sets `Ninja.isInvisble = false`
- Duration counter decremented each frame in UpdatePatch.cs:259 (`Ninja.invisibleTimer -= dt`)

#### Arrow Updates (NinjaTrace.cs and Objects/Arrow.cs)
See NinjaTrace section below.

### Options Table

| Option Name | Option ID | Type | Default | Min | Max | Step |
|---|---|---|---|---|---|---|
| Ninja Spawn Rate | 380 | Dropdown | - | - | - | - |
| Ninja Mark Cooldown | 381 | Float | 30.0s | 10.0s | 120.0s | 5.0s |
| Ninja Knows Location Of Target | 382 | Bool | true | - | - | - |
| Trace Duration | 383 | Float | 5.0s | 1.0s | 20.0s | 0.5s |
| Time Till Trace Color Has Faded | 384 | Float | 2.0s | 0.0s | 20.0s | 0.5s |
| Time The Ninja Is Invisible | 385 | Float | 3.0s | 0.0s | 20.0s | 1.0s |

**Note:** "Trace Color Has Faded" controls how long the trace displays in the ninja's color before fading to transparent green, independent of trace duration.

### Networking (RPC)

#### RPC Enum Values
- `PlaceNinjaTrace = 131` (CustomRPC enum, RPC.cs:131)
- `SetInvisible = 147` (CustomRPC enum, RPC.cs:147)
- `ShareGhostInfo = 173` (CustomRPC enum, RPC.cs:173) — with subtype `GhostInfoTypes.NinjaMarked = 15`
- `UncheckedMurderPlayer = 97` (CustomRPC enum, RPC.cs:97)
- `ShareGhostInfo = 173` (for marking sync)

#### PlaceNinjaTrace (RPC.cs:836-843)
**Payload:** `byte[]` buffer containing two floats (8 bytes total):
- Bytes 0-3: x position (float)
- Bytes 4-7: y position (float)

**Handler:** `placeNinjaTrace(byte[] buff)`
- Creates new `NinjaTrace` object at position with duration = `Ninja.traceTime`
- Clears `Ninja.ninjaMarked = null` for non-ninja players (hide marked state on death)

#### SetInvisible (RPC.cs:845-869)
**Payload:** `byte playerId, byte flag`
- `playerId`: target player to apply invisibility to
- `flag`: 0 (make invisible) or 255 / byte.MaxValue (restore visibility)

**Handler:** `setInvisible(byte playerId, byte flag)`
- Retrieves player by ID
- If flag == 255: restore normal appearance (white body, default hat/skin/pet, colorblind text visible)
- If flag == 0: make invisible
  - Sets all cosmetics to empty strings (empty name, hat ID "6", empty skin/pet)
  - Sets body color to transparent (alpha = 0)
  - If local player is impostor or dead: alpha = 0.1f (can see faintly)
  - Disables colorblind text display
  - Sets `Ninja.invisibleTimer` and `Ninja.isInvisble` flags

#### ShareGhostInfo with NinjaMarked subtype (RPC.cs:1529-1530, GhostInfoTypes.NinjaMarked = 15)
**Payload:** `byte playerId, byte infoType (15), byte markedPlayerId`

**Handler:** Reads marked player ID and sets `Ninja.ninjaMarked = Helpers.playerById(markedPlayerId)`

#### UncheckedMurderPlayer (Helpers.cs and RPC.cs)
Standard murder RPC with flag byte for animation:
- Flag 255 (byte.MaxValue): show kill animation and teleport
- Any other flag: instant/no animation

### Edge Cases in Code

1. **Medic Shield:** If target has Medic shield, `checkMuderAttempt` returns `SuppressKill`, button timer resets to 0 (no cooldown applied), assassination fails silently.

2. **Mini Modifier:** If target is Mini and not grown up, `checkMuderAttempt` returns `SuppressKill`.

3. **TimeMaster Shield:** If TimeMaster has active shield, returns `SuppressKill` and triggers rewind RPC.

4. **Armored Modifier:** If target is Armored, returns `BlankKill` (cooldown applies but target not killed).

5. **Thief Interaction:** If Ninja is actually a Thief who stole Ninja role, the thief's crew-only kill restriction applies — if ninja is thief and tries to kill impostor, thief dies instead (if setting allows), assassination fails.

6. **Lovers:** No special handling in code; if lover dies, lover's partner dies via separate Lovers mechanic (independent of Ninja).

7. **Vents/Transportation:** If target is using vent or transportation tool, `checkMuderAttempt` returns `SuppressKill`.

8. **Meeting During Invisibility:** When meeting starts, invisibility timer continues to count down. If meeting ends before timer expires, ninja remains invisible for remainder of duration. No special handling to clear invisibility on meeting end.

9. **Target Death Mid-Mark:** If marked target dies before assassination button is pressed, `checkMuderAttempt` returns `SuppressKill` (target is dead).

10. **Camera/Admin:** 
    - Ninja appears invisible on camera feeds during invisibility (body color = clear/0.1f alpha, empty name)
    - Arrow does NOT appear on admin/camera feeds (client-side UI only)
    - Trace visible on all screens (shared game object)

11. **Lights Out (Mushroom Sabotage):** Ninja invisibility persists through lights out/night vision mode (UsablesPatch.cs:657-692 skips ninja during night vision setup).

12. **Submerged Map:** On Submerged, floor is changed on assassination to match target's floor.

### NinjaTrace.cs Details (Objects/NinjaTrace.cs)

**Constructor:** `NinjaTrace(Vector2 p, float duration=1f)`

**Rendering:**
- Creates GameObject "NinjaTrace" with SpriteRenderer component
- Sprite: `TheOtherRoles.Resources.NinjaTraceW.png` (200f scale)
- Z-position: `p.y / 1000f + 0.01f` (sorts by y-coordinate for depth)
- Submerged compatibility: adds ElevatorMover component for floor transitions

**Color Animation (Lines 35-48):**
- Duration: `CustomOptionHolder.ninjaTraceColorTime.getFloat()` (default 2.0s)
- Starts at ninja's player color (or white if Lighter, or dark if neither)
- Ends at green (Color.green)
- Interpolation: `lerp(p) = (1-p) * ninja_color + p * green`
- Updates via coroutine every frame

**Fade-Out Animation (Lines 50-58):**
- Fade duration: `min(1.0, 0.5 * trace_duration)` (max 1s, or half the trace time)
- Stays fully opaque for `(trace_duration - fade_duration) / trace_duration` of the time
- Then fades alpha from 1 to 0 over remaining time
- Interpolation: `alpha_interp = (elapsed * duration + fadeOutDuration - duration) / fadeOutDuration` for last phase

**Lifecycle:**
- `UpdateAll()` called every fixed update (UpdatePatch.cs)
  - Decrements `timeRemaining -= Time.fixedDeltaTime`
  - When timeRemaining < 0: deactivates and destroys GameObject, removes from traces list
  - Line 68-79: `NinjaTrace.cs`

**Visibility:**
- Visible on all clients (shared game object)
- Visible on camera/admin feeds (regular sprite renderer)
- Persists until timeout (meetings do not clear traces)

### Assets

**Sprite Resources:**
- Mark button: `TheOtherRoles.Resources.NinjaMarkButton.png` (115f scale) — loaded in `Ninja.getMarkButtonSprite()`
- Assassinate button: `TheOtherRoles.Resources.NinjaAssassinateButton.png` (115f scale) — loaded in `Ninja.getKillButtonSprite()`
  - Button sprite swaps based on `Ninja.ninjaMarked` state
- Trace: `TheOtherRoles.Resources.NinjaTraceW.png` (225f scale) — loaded in `NinjaTrace.getTraceSprite()`
- Arrow: `TheOtherRoles.Resources.Arrow.png` (200f scale) — loaded in `Arrow.getSprite()`

**Audio:**
- Mark sound: `"warlockCurse"` played via `SoundEffectsManager.play()` (Buttons.cs:1711)

---

## Witch Role

### Role State
**Files and Locations:**
- State: `TheOtherRoles/TheOtherRoles.cs:1597-1639` (static class `Witch`)

**Fields:**
- `witch`: `PlayerControl` - the witch player instance (null when not assigned)
- `futureSpelled`: `List<PlayerControl>` - list of players marked to die at meeting end
- `currentTarget`: `PlayerControl` - the closest targetable player within kill distance (refreshed each frame)
- `spellCastingTarget`: `PlayerControl` - the player currently being spelled (set when cast button activated, used during channel duration)
- `color`: `Palette.ImpostorRed` - witch team color
- `cooldown`: `float` - spell cooldown in seconds (default 30f)
- `spellCastingDuration`: `float` - how long the spell must be held before target is marked (default 1f, called "casting" duration)
- `cooldownAddition`: `float` - additional cooldown added each time a spell is cast (default 10f)
- `currentCooldownAddition`: `float` - cumulative additional cooldown (reset at meeting)
- `canSpellAnyone`: `bool` - if true, can spell impostors; if false, only crewmates (default false)
- `triggerBothCooldowns`: `bool` - if true, each spell cast also sets witch's kill cooldown; if false, only spell cooldown affected (default true)
- `witchVoteSavesTargets`: `bool` - if true, voting out the witch clears all `futureSpelled` targets (saves them) (default true)

**Lifecycle:**
- `clearAndReload()` called at: role reset, game start (initializes to defaults from `CustomOptionHolder`)
  - Line 1627-1638: `TheOtherRoles.cs`

### Full Ability Flow

#### Button Targeting Rules (Lines 848-849, PlayerControlPatch.cs)
- `Witch.currentTarget = setTarget(onlyCrewmates: !Witch.canSpellAnyone, ...)`
- If `canSpellAnyone = false`: only targets crewmates
- If `canSpellAnyone = true`: targets anyone (including impostors)
- Same targeting rules as Ninja: closest within KillDistance, no obstacles, not dead/disconnected
- Each frame updates target highlight outline in witch's color

#### Spell Cast Phase (Lines 1599-1625, Buttons.cs)
1. **Trigger condition:** player presses button when `Witch.currentTarget != null`
2. **Immediate actions:**
   - Set `Witch.spellCastingTarget = Witch.currentTarget`
   - Play sound `"witchSpell"`
   - Button becomes active effect button (enters "channeling" state)

3. **Channel phase:**
   - Button effect duration set to `Witch.spellCastingDuration` (default 1.0s) via `EffectDuration` property (Line 170, Buttons.cs)
   - While channeling, if target changes, spell is cancelled (Line 1608): `if (witchSpellButton.isEffectActive && Witch.spellCastingTarget != Witch.currentTarget)`
     - Resets button timer, clears channel, sets `spellCastingTarget = null`
   - Update check (Line 1613): can only cast if alive, `PlayerControl.LocalPlayer.CanMove`, and current target exists

4. **Spell Completion (Lines 1626-1648) — after channel duration expires:**
   - If `spellCastingTarget == null`: do nothing (cancelled during channel)
   
   - If `spellCastingTarget != null` (Lines 1627-1647):
     - **Murder attempt check** (Line 1628):
       - `checkMuderAttempt(Witch.witch, Witch.spellCastingTarget)` evaluates same conditions as Ninja
       - Returns `PerformKill`, `BlankKill`, or `SuppressKill`
     
     - **If `PerformKill` (Lines 1629-1633):**
       - Send `CustomRPC.SetFutureSpelled` with target's player ID
       - Calls `setFutureSpelled(target.PlayerId)`: adds target to `Witch.futureSpelled` list if not already present
       - Target not killed immediately; marked for death at meeting end
     
     - **Apply cooldowns (Lines 1635-1643):**
       - If `PerformKill` or `BlankKill`:
         - Add `Witch.cooldownAddition` to `Witch.currentCooldownAddition` (cumulative)
         - Set button's MaxTimer to `Witch.cooldown + Witch.currentCooldownAddition` (cooldown increases with each cast)
         - If `triggerBothCooldowns == true`: also set witch's `killTimer` to vanilla kill cooldown (usually 25s)
       - If only `BlankKill`: cooldowns apply but target not marked
     
     - **If `SuppressKill` (Lines 1644-1646):**
       - Reset button timer to 0 (no cooldown, attempt again immediately)
     
     - Clear `Witch.spellCastingTarget = null` regardless of outcome

#### Spell Execution at Meeting End (ExileControllerPatch.cs:56-88, after player voted out)

**Trigger:** Meeting ends, ExileController calls `OnDestroy()` prefix

**Logic:**
1. Check if witch is alive and has targets (Lines 57-61):
   - If exiled player == witch OR witch dies from lover mechanics AND `witchVoteSavesTargets == true`:
     - Clear `Witch.futureSpelled` list (all targets saved/protected)
   
2. For each remaining player in `futureSpelled` (Lines 62-86):
   - Verify target not dead and `checkMuderAttempt(Witch.witch, target, blockRewind=true)` returns `PerformKill`
   - Check Lawyer protection (if target is lawyer or lover, skip based on conditions)
   - If target == Lawyer.target: promote lawyer to pursuer
   - Send `CustomRPC.UncheckedExilePlayer` (not murder, exile) — target dies as if voted out
   - Send `CustomRPC.ShareGhostInfo` with death reason `DeathReason.WitchExile` and killer = witch
   - Record in GameHistory with custom death reason

3. After all spells executed (Line 88):
   - Clear `Witch.futureSpelled = new List<PlayerControl>()` (resets for next meeting)

**Note:** Spells persist across multiple meetings; only the final meeting when exiled does `witchVoteSavesTargets` apply.

#### Meeting UI Display (MeetingPatch.cs:600-611, MeetingHud Update)

When meeting HUD is created:
1. If witch exists and `futureSpelled` list is populated:
2. For each player in meeting vote area:
   - If player is in `futureSpelled` list:
     - Add overlay sprite (`Witch.getSpelledOverlaySprite()`) to player's vote area
     - Overlay positioned at `(-0.5f, -0.03f, -1f)` (left side of player name, behind other UI)
     - If local player is Swapper+Guesser: adjust position to `(-0.725f, -0.15f, -1f)` to avoid conflict

**Overlay Sprite:**
- `TheOtherRoles.Resources.SpellButtonMeeting.png` (225f scale)
- Visible to all players in meeting (shows who is spelled)

### Options Table

| Option Name | Option ID | Type | Default | Min | Max | Step |
|---|---|---|---|---|---|---|
| Witch Spawn Rate | 370 | Dropdown | - | - | - | - |
| Witch Spell Casting Cooldown | 371 | Float | 30.0s | 10.0s | 120.0s | 5.0s |
| Witch Additional Cooldown | 372 | Float | 10.0s | 0.0s | 60.0s | 5.0s |
| Witch Can Spell Anyone | 373 | Bool | false | - | - | - |
| Spell Casting Duration | 374 | Float | 1.0s | 0.0s | 10.0s | 1.0s |
| Trigger Both Cooldowns | 375 | Bool | true | - | - | - |
| Voting The Witch Saves All The Targets | 376 | Bool | true | - | - | - |

**Option Semantics:**
- **Witch Spell Casting Cooldown:** Base cooldown before each spell; increases by "Additional Cooldown" per cast
- **Witch Additional Cooldown:** Added to cooldown cumulatively per spell cast during a game (resets at meeting)
- **Witch Can Spell Anyone:** If false, can only spell crewmates; if true, can spell anyone including impostors
- **Spell Casting Duration:** How long button must be held/active effect active before spell completes and target is marked
- **Trigger Both Cooldowns:** If true, each spell also sets witch's kill cooldown to vanilla value; if false, only spell cooldown affected
- **Voting The Witch Saves All The Targets:** If true and witch (or witch's lover) is exiled, all spelled targets are saved (not killed at meeting end)

### Networking (RPC)

#### RPC Enum Values
- `SetFutureSpelled = 130` (CustomRPC enum, RPC.cs:130)
- `UncheckedExilePlayer = 99` (CustomRPC enum, RPC.cs:99)
- `ShareGhostInfo = 173` (CustomRPC enum, RPC.cs:173) — with subtype `GhostInfoTypes.DeathReasonAndKiller`
- `LawyerPromotesToPursuer = 141` (CustomRPC enum, RPC.cs:141)

#### SetFutureSpelled (RPC.cs:827-834)
**Payload:** `byte playerId`

**Handler:** `setFutureSpelled(byte playerId)`
- Retrieves player by ID
- If `Witch.futureSpelled == null`: initialize as empty list
- Add player to `futureSpelled` list (no duplicate checks in code, relies on caller)

#### UncheckedExilePlayer (during meeting end)
Standard exile RPC (not murder):
- Called for each spelled target at meeting end
- Marks target as dead with exile animation

#### ShareGhostInfo with DeathReasonAndKiller subtype
**Payload:** `byte playerId, byte infoType (16), byte playerId, byte deathReason (10 = WitchExile), byte killerId`

**Handler:** Records death reason and killer in GameHistory

#### LawyerPromotesToPursuer (if target is lawyer)
Triggers lawyer -> pursuer transformation if lawyer's target was voted out

### Edge Cases in Code

1. **Can Spell Anyone = false (default):** Only crewmates can be spelled. Impostors/Ninja/Witch cannot be spelled. Targeting rules enforced by `setTarget(onlyCrewmates: !canSpellAnyone)`.

2. **Medic Shield:** If target has Medic shield, `checkMuderAttempt` returns `SuppressKill`, spell fails silently, no cooldown applied.

3. **Mini Modifier:** If target is Mini and not grown up, spell fails; no cooldown applied.

4. **TimeMaster Shield:** If TimeMaster has active shield, spell fails with rewind RPC.

5. **Thief Interaction:** If witch is actually a thief who stole witch role, crew-only kill restriction applies to spell casting.

6. **Lovers:** No special handling for lovers except: if witch dies via lover mechanics (both_die = true, partner exiled), and `witchVoteSavesTargets = true`, targets are saved.

7. **Lawyer Protection:** If spelled target is lawyer or lover, Lawyer.target was the exiled player, and lawyer is prosecutor: spell is skipped. If target == Lawyer.target: lawyer promoted to pursuer.

8. **Vents/Transportation:** If target is using vent/transportation, `checkMuderAttempt` returns `SuppressKill`.

9. **Channel Cancellation:** If target moves away or is killed during the `spellCastingDuration` channel, spell is cancelled only if `currentTarget` changes (Line 1608). No range check during channel; only active target check.

10. **Target Death Mid-Channel:** If target dies during channel, spell is NOT automatically cancelled. Only cancelled if `spellCastingTarget != Witch.currentTarget` (e.g., another player becomes closest target). If target stays marked despite dying, `checkMuderAttempt` will return `SuppressKill` when execution occurs.

11. **Multiple Spells:** Witch can mark multiple targets across multiple casts. All marked targets die together at the end of the next meeting.

12. **Voting Witch:** 
    - Witch voted out AND `witchVoteSavesTargets = true`: all targets saved (cleared)
    - Witch voted out AND `witchVoteSavesTargets = false`: all targets still die at exile
    - Witch not voted out: targets die regardless

13. **Witch Dies During Meeting (not voted):** If witch is killed/dies by other means during meeting (e.g., by a killer), `witchVoteSavesTargets` does NOT apply (only applies if witch is exiled). Targets still die.

14. **Lover Saves Witch:** If witch's lover is exiled and `Lovers.bothDie = true`, witch dies. `witchVoteSavesTargets` applies (targets saved). If `Lovers.bothDie = false`, witch survives and targets still die.

15. **Cumulative Cooldown Reset:** `currentCooldownAddition` is NOT reset after each spell; only resets at game start or meeting start (to be verified, but likely in clearAndReload or meeting start logic).

16. **Cooldown on Failed Spell:** If `BlankKill` or `SuppressKill`, cooldown still applies. Only difference is target not marked.

17. **Camera/Admin:** Marked targets visible in meeting as overlaid star sprite. During non-meeting gameplay, no special indicator visible on camera/admin (spell status is abstract).

18. **Trigger Both Cooldowns Semantics:** If true, each spell sets witch's kill cooldown (normal impostor kill cooldown). This doesn't prevent witch from using kill button; it's an additional penalty on top of spell cooldown.

### Assets

**Sprite Resources:**
- Spell button: `TheOtherRoles.Resources.SpellButton.png` (115f scale) — loaded in `Witch.getButtonSprite()`
- Spelled overlay (meeting): `TheOtherRoles.Resources.SpellButtonMeeting.png` (225f scale) — loaded in `Witch.getSpelledOverlaySprite()`

**Audio:**
- Spell cast sound: `"witchSpell"` played via `SoundEffectsManager.play()` (Buttons.cs:1603)

---

## Other Roles: Sniper, Pelican, Astral

**Presence in TheOtherRoles:** None. These roles are not defined in the TOR codebase.

---

## Summary of Implementation Quirks & Trickiest Details for Porters

### Ninja

1. **Two-Button System with State Machine:** Ninja uses one button that toggles between "mark" and "assassinate" modes based on `Ninja.ninjaMarked` state. The button sprite changes dynamically (Buttons.cs:1723). Many ports may expect two separate buttons; TOR consolidates into one button with state tracking.

2. **Trace Placement Before and After Kill:** The assassination creates TWO traces:
   - One at ninja's current position BEFORE the murder RPC (Lines 1660-1668)
   - One at target's position AFTER the murder RPC (Lines 1687-1696)
   - Both use the same `PlaceNinjaTrace` RPC, but order matters for visibility and synchronization.

3. **Invisibility Rendered as Empty Cosmetics:** The invisibility is not a shader effect or alpha blend for all viewers. Instead, the ninja's cosmetics (name, hat, skin, pet) are set to empty strings, making them truly invisible in-game. Impostors and dead players see a faint ghost (alpha 0.1f). This is different from typical "invisibility" implementations and has implications for how other systems (like name displays, roles, etc.) interact.

4. **Arrow Not on Admin/Camera:** The arrow pointing to the marked target is a client-side UI object and does NOT render on camera or admin feeds. This is a design choice; many ports might expect the arrow to be visible on surveillance. The trace IS visible on all screens.

5. **Invisibility Timer Countdown Independent of Meeting:** When a meeting occurs during invisibility, the timer continues to count down. No special logic clears invisibility when meeting starts or ends. This can lead to unexpected scenarios where ninja is invisible during the meeting UI (though ninja is sent to meeting lobby, so practically invisible state is irrelevant during meeting).

### Witch

1. **Delayed Execution at Meeting End:** Unlike instant kill roles, witch's spell executions happen at the very end of the meeting in the `ExileController.OnDestroy` prefix. They are called via `UncheckedExilePlayer` RPC, which exile-deaths the targets (not murder-deaths). This means witch death reason is `WitchExile`, not `Kill`, which affects statistics and role reveal UI.

2. **Cumulative Cooldown Mechanic:** The cooldown increases by `cooldownAddition` EACH time a spell is successfully cast. This is rare in Among Us mods; most roles have fixed cooldowns. The `currentCooldownAddition` variable stores the cumulative value and is used to compute the next button timer. It's unclear from code alone if this resets at each meeting or persists; likely resets at game start (in `clearAndReload`).

3. **Voting Saves Targets is Asymmetric:** The `witchVoteSavesTargets` option is misleading. It does NOT save targets just because the witch votes or targets vote for themselves. It saves targets specifically when:
   - The witch is exiled (voted out by majority), OR
   - The witch dies due to a lover's exile (and Lovers.bothDie = true)
   - AND the flag is true
   
   If witch is killed by a killer during the meeting (not voted), targets are NOT saved.

4. **Channel Cancellation Logic:** The spell channel can be cancelled if the targeted player's position changes significantly (becomes not the `currentTarget` anymore). However, this is checked ONLY during the effect-active phase (Line 1608), not during the cooldown phase. If the target stays `currentTarget` throughout the channel, the spell completes even if the target moved.

5. **Target Marking Happens at Meeting End, Not Cast Time:** Spelling a player marks them for death, but they don't actually die until the meeting ends and the murder is executed. This means:
   - Spelled players are vulnerable to other kills before the meeting ends.
   - Spelled players can kill the witch during the meeting and still die to the witch's spell after exiling the witch (depending on `witchVoteSavesTargets`).
   - Spelled players remain marked across multiple rounds if witch doesn't cast again.

6. **Overlay Appears Only in Meetings:** The spell indicator (star overlay) only appears during the meeting UI. Outside of meetings, there is no visual indication of who is spelled (other than the witch knowing).

---

## Code Path Summary for Quick Reference

### Ninja Critical Files
- State: `TheOtherRoles.cs:1641-1682`
- Options: `CustomOptionHolder.cs:513-517`
- Buttons: `Buttons.cs:1652-1734`
- Update loop: `PlayerControlPatch.cs:363-398` (ninjaUpdate)
- Arrow targeting: `PlayerControlPatch.cs:373-398`
- Invisibility RPC: `RPC.cs:845-869` (setInvisible)
- Trace spawn: `RPC.cs:836-843` (placeNinjaTrace)
- Trace rendering: `Objects/NinjaTrace.cs:20-62`
- Invisibility timer: `UpdatePatch.cs:259`
- Lights out handling: `UsablesPatch.cs:657-692`

### Witch Critical Files
- State: `TheOtherRoles.cs:1597-1639`
- Options: `CustomOptionHolder.cs:505-510`
- Buttons: `Buttons.cs:1599-1649`
- Targeting: `PlayerControlPatch.cs:848-849`
- Spell execution: `ExileControllerPatch.cs:56-88`
- Meeting UI: `MeetingPatch.cs:600-611`
- Spell RPC: `RPC.cs:827-834` (setFutureSpelled)
