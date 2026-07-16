# TheOtherRoles Eraser and Vulture Role Documentation

## Open Questions for Implementer

Before beginning port, clarify with stakeholders:

1. **Eraser role-change semantics**: When a crewmate is erased and becomes impostor (or vice versa), should the erased player:
   - Be notified they were erased (e.g., chat message, UI flash)?
   - Have their tasks retained or cleared?
   - Be teleported to a neutral position, or stay where they are?

2. **Vulture win timing**: Should Vulture's win condition trigger:
   - Immediately when the Nth body is eaten (like TOR)?
   - At end of meeting after bodies resolved (TOU-style)?
   - With any special animation or notification?

3. **Erased target already-dead edge case**: If Eraser targets a dead player, should:
   - The button press fail silently?
   - Consume cooldown but do nothing?
   - Allow erasing even dead players (TOR allows it)?

4. **Vulture + Janitor/body cleanup**: When Janitor cleans bodies during rounds, do eaten-body counts still accumulate?
   - TOR: Yes, Janitor can eat already-eaten bodies (double-count prevention via cleaner role check)
   - Recommendation: Clarify if port should match this or simplify to "one clean per body"

5. **Eraser + Spy/Lovers**: 
   - Can Eraser erase Spy (if canEraseAnyone = false)?
   - Can Eraser erase lovers (both or just one)?

---

## Eraser Role

### Role State
**Files and Locations:**
- State: `TheOtherRoles/TheOtherRoles.cs:969-995` (static class `Eraser`)

**Fields:**
- `eraser`: `PlayerControl` - the eraser player instance (null when not assigned)
- `futureErased`: `List<PlayerControl>` - list of players marked to have their roles erased at next meeting end
- `currentTarget`: `PlayerControl` - the closest targetable player within kill distance (refreshed each frame)
- `alreadyErased`: `List<byte>` - list of player IDs whose roles have been erased this round (used to prevent Guesser from targeting them again)
- `color`: `Palette.ImpostorRed` - eraser team color
- `cooldown`: `float` - base erase cooldown in seconds (default 30f)
- `canEraseAnyone`: `bool` - if true, can erase impostors; if false, only crewmates (default false)

**Lifecycle:**
- `clearAndReload()` called at: role reset, game start (initializes to defaults from `CustomOptionHolder`)
  - Line 987-994: `TheOtherRoles.cs`
  - Resets `eraser`, `futureErased`, `currentTarget` to null/empty
  - Resets `alreadyErased` to empty list
  - Loads `cooldown` and `canEraseAnyone` from options

### Full Ability Flow

#### Button Targeting Rules (Lines 275-283, PlayerControlPatch.cs)
- `Eraser.currentTarget = setTarget(onlyCrewmates: !Eraser.canEraseAnyone, untargetablePlayers: ...)`
- **If canEraseAnyone = false (default):**
  - Only targets crewmates (impostors cannot be targeted)
  - Cannot target: Spy, Sidekick (if was Impostor team), Jackal (if was Impostor team)
- **If canEraseAnyone = true:**
  - Targets anyone (impostors can be erased)
  - Cannot target: Spy, Sidekick (if was Impostor team), Jackal (if was Impostor team)
- **Same restrictions apply to all targets:** Self, dead players, disconnected players, players in vents, untargetable players (e.g., shielded by Medic)
- **Target cannot be erased if:**
  - Player == Jackal.jackal OR
  - Player == Sidekick.sidekick OR
  - Player in Jackal.formerJackals (ex-Jackal players)
  - (Line 229, Helpers.cs: `canBeErased()` check)
- Each frame updates target highlight outline in eraser's color

#### Erase Button Click (Lines 1100-1119, Buttons.cs)
1. **Trigger condition:** player presses button and `Eraser.currentTarget != null`
2. **Actions immediately:**
   - Increment button's MaxTimer by 10 seconds (`eraserButton.MaxTimer += 10`) — cumulative cooldown increase
   - Reset button Timer to new MaxTimer value
   - Send `CustomRPC.SetFutureErased` with target's player ID to sync across clients (Line 1106-1108)
   - Call `RPCProcedure.setFutureErased(target.PlayerId)` locally (Line 1109)
   - Play sound "eraserErase" (Line 1110)

#### SetFutureErased RPC (RPC.cs:809-816)
**Payload:** `byte playerId`

**Handler:** `setFutureErased(byte playerId)`
- Retrieves player by ID
- Initializes `Eraser.futureErased` list if null (Line 811-812)
- Adds player to `futureErased` list (no duplicate prevention in code; caller responsible)

#### Erase Execution at Meeting End (ExileControllerPatch.cs:36-46, BeginForGameplay Prefix)

**Trigger:** Meeting ends, ExileController.BeginForGameplay() is called (BEFORE exile animation)

**Sequence (host-only, Line 37: `AmongUsClient.Instance.AmHost`):**
1. **For each player in `Eraser.futureErased` list** (Lines 38-45):
   - Send `CustomRPC.ErasePlayerRoles` with target player ID (Line 39-41)
   - Call `RPCProcedure.erasePlayerRoles(target.PlayerId)` locally (Line 42)
   - Add target player ID to `Eraser.alreadyErased` list (Line 43)

2. **After all erasures** (Line 46):
   - Clear `Eraser.futureErased = new List<PlayerControl>()`

#### ErasePlayerRoles RPC (RPC.cs:733-807)
**Payload:** `byte playerId`

**Handler:** `erasePlayerRoles(byte playerId, bool ignoreModifier = true)`
- Validates target player can be erased via `player.canBeErased()` (Line 735)
- **If erased player was any role**, calls that role's `clearAndReload()`:
  - Lines 738-770: All crewmate, impostor, and other role options checked
  - Each role's state is reset to null/defaults
  - **Key insight**: If erased player was Eraser, calls `Eraser.clearAndReload()` (Line 765), removing them from being eraser
  - If erased player was Vulture, calls `Vulture.clearAndReload()` (Line 787), clearing eaten body count
- **If ignoreModifier = false** (Line 793):
  - Also clears modifiers like Lovers, Bait, Bloody, Anti-Teleport, Sunglasses, Tiebreaker, Mini, VIP, Invert, Chameleon, Armored (Lines 795-805)
  - Note: Default call has `ignoreModifier = true`, so modifiers are NOT cleared when erasing via Eraser button

**Result of Erase:**
- **If crewmate erased**: Becomes crewmate without a role (vanilla crewmate with tasks intact)
- **If impostor erased**: Becomes crewmate (!!!) — role is stripped but team changes to crewmate
- **Modifiers retained** (default): Any modifiers like Lovers, Mini, etc. remain on the player
- **Tasks:** NOT explicitly cleared; remain as-is (if impostor erased, crewmate tasks appear; if crewmate with tasks erased, tasks remain)

### Options Table

| Option Name | Option ID | Type | Default | Min | Max | Step |
|---|---|---|---|---|---|---|
| Eraser Spawn Rate | 230 | Dropdown | - | - | - | - |
| Eraser Cooldown | 231 | Float | 30.0s | 10.0s | 120.0s | 5.0s |
| Eraser Can Erase Anyone | 232 | Bool | false | - | - | - |

**Cooldown Behavior:**
- Base cooldown = `CustomOptionHolder.eraserCooldown` (default 30s)
- **Each successful erase increases cooldown by 10s** (cumulative within round)
- Example: 30s → 40s → 50s → 60s ...
- Cooldown persists across multiple rounds (no reset at meeting start, only at game start)

### Networking (RPC)

#### RPC Enum Values
- `SetFutureErased = 127` (CustomRPC enum, RPC.cs:127)
- `ErasePlayerRoles = 126` (CustomRPC enum, RPC.cs:126)

#### SetFutureErased (RPC.cs:809-816)
Already documented above in "Erase Execution at Meeting End"

#### ErasePlayerRoles (RPC.cs:733-807)
Already documented above

### Edge Cases in Code

1. **Cannot Erase Certain Roles:**
   - Jackal and Sidekick cannot be erased (Helpers.cs:229, `canBeErased()` check)
   - Former Jackals cannot be erased
   - If attempting to erase these players, `erasePlayerRoles()` returns early with no change

2. **Eraser Erased:**
   - If Eraser is targeted and erased, `Eraser.clearAndReload()` is called, removing them as eraser
   - Eraser will remain alive but lose their role (become crewmate)
   - Future erase buttons will not work (Eraser.eraser == null check fails)

3. **Medic Shield:** If target has Medic shield:
   - Target can still be marked in `setFutureErased()` (no shield check at mark time)
   - However, `checkMuderAttempt()` is NOT called for Eraser, so shield doesn't block erase
   - Erase proceeds regardless of shield

4. **Mini Modifier:** No special handling; Mini's growth/shrinkage doesn't affect erase mechanics.

5. **Lovers:**
   - **If one lover is erased**, only that lover's role is erased; the other lover's role remains intact
   - **If both lovers happen to be erased separately**, both lose their roles but remain linked (both become crewmates)
   - Lovers mechanic is NOT cleared when one lover is erased (modifiers NOT cleared by default)

6. **Eraser + Spy:**
   - If `canEraseAnyone = false`, Spy cannot be targeted (untargetable list, Line 278)
   - If `canEraseAnyone = true`, Spy CAN be erased (no restriction in `canBeErased()`)

7. **Erased Target Already Dead:**
   - If target is already dead when erase resolves at meeting end:
     - `erasePlayerRoles()` still runs (no dead player check at execution time)
     - Role is cleared, but player remains dead
     - No game impact beyond role removal

8. **Guesser Interaction (MeetingPatch.cs:621):**
   - In meetings, Guesser cannot guess players in `Eraser.alreadyErased` list
   - Only affects Eraser's own view (guessing own erased targets is prevented)
   - Other guessers (if multiple roles) can still guess erased players

9. **Meeting During Erase Mark:**
   - If marked target dies before the meeting ends (normal death, not erase), they're still in `futureErased` list
   - Erase resolves even on dead players (as noted above)

10. **Multiple Erasures:**
    - Eraser can mark multiple targets across multiple button presses
    - All marked targets have roles erased together at meeting end
    - Cooldown accumulates with each erase

11. **Cumulative Cooldown Resets:**
    - Cooldown does NOT reset at meeting start
    - Only resets at game start (in `clearAndReload()`)
    - Cumulative cooldown persists across entire round

12. **Camera/Admin:**
    - Erased player's role is no longer visible on admin/camera feeds (role icon removed)
    - Erased player's tasks appear/change based on new role (crewmate tasks if erased from impostor)
    - Erased player's appearance (name, color, outfit) unchanged by erase itself (role-stripped only)

13. **Thief Interaction:**
    - If Eraser is actually a Thief who stole Eraser role, no special handling documented
    - Assumes Eraser button works normally for thief

14. **Vents/Transportation:**
    - Target in vent/using transportation can still be marked for erase
    - No vent/transportation check during mark phase
    - Erase resolves at meeting end regardless of vent state

15. **Erasing Impostor:**
    - If impostor is erased (and `canEraseAnyone = true`), they become a **crewmate** (not impostor)
    - This can flip the kill-voting balance dramatically
    - Impostor tasks are removed; crewmate tasks appear (if erased impostor had no tasks before, now has crewmate tasks)

### Assets

**Sprite Resources:**
- Erase button: `TheOtherRoles.Resources.EraserButton.png` (115f scale) — loaded in `Eraser.getButtonSprite()`

**Audio:**
- Erase sound: `"eraserErase"` → `assets/audio/erasererase.ogg` (via SoundEffectsManager, loaded from ToRAudio assetbundle)

---

## Vulture Role

### Role State
**Files and Locations:**
- State: `TheOtherRoles/TheOtherRoles.cs:1347-1379` (static class `Vulture`)

**Fields:**
- `vulture`: `PlayerControl` - the vulture player instance (null when not assigned)
- `localArrows`: `List<Arrow>` - arrows pointing toward dead bodies visible to vulture (synchronized each frame with current bodies)
- `color`: `Color32(139, 69, 19, ...)` - brown color for vulture
- `cooldown`: `float` - body-eat cooldown in seconds (default 15f)
- `eatenBodies`: `int` - count of bodies eaten so far this round (incremented each time vulture eats a body)
- `vultureNumberToWin`: `int` - number of bodies vulture must eat to trigger win (default 4)
- `triggerVultureWin`: `bool` - flag set to true when `eatenBodies >= vultureNumberToWin`
- `canUseVents`: `bool` - if true, vulture can enter vents (vanilla impostor vent ability) (default true)
- `showArrows`: `bool` - if true, arrows point toward dead bodies (default true)

**Lifecycle:**
- `clearAndReload()` called at: role reset, game start, role erase (initializes to defaults from `CustomOptionHolder`)
  - Line 1364-1378: `TheOtherRoles.cs`
  - Resets `vulture`, `eatenBodies` to 0, `triggerVultureWin` to false
  - Resets `vultureNumberToWin`, `cooldown`, `canUseVents`, `showArrows` from options
  - Destroys and clears all `localArrows`

### Full Ability Flow

#### Body Detection & Arrow Rendering (PlayerControlPatch.cs:717-741, vultureArrowsUpdate())
**Update every frame when vulture is alive and in-game:**

1. **Early exit conditions** (Line 717):
   - Return if: Vulture is null OR local player is not vulture OR arrows list is null OR showArrows is false

2. **Dead vulture check** (Lines 718-721):
   - If vulture is dead: Destroy all arrows and clear list, return

3. **Find all dead bodies** (Line 724):
   - `DeadBody[] deadBodies = UnityEngine.Object.FindObjectsOfType<DeadBody>()`
   - Searches entire game scene for dead body objects (includes reported/unreported bodies)

4. **Arrow synchronization check** (Lines 725-730):
   - Compare current arrow count vs. number of dead bodies found
   - If counts differ (bodies added/removed):
     - Destroy all existing arrows
     - Create NEW arrows (resets synchronization)

5. **Create/Update arrows** (Lines 733-740):
   - **For each dead body**:
     - If first iteration (arrowUpdate = true): Create new blue Arrow object and add to `localArrows`
     - Update arrow position to point from vulture toward body position
     - Arrow color: hardcoded `Color.blue` (not vulture's brown color)

**Arrow rendering:**
- Uses `Arrow` class (Objects/Arrow.cs)
- Has `ArrowBehaviour` component that updates rotation to face dead body
- Color: Blue (Line 735)
- Sprite: `TheOtherRoles.Resources.Arrow.png` at 200f pixels per unit
- Layer: 5 (UI layer)
- Updates every frame via `Vulture.localArrows[index].Update(db.transform.position)` (Line 738)
- Does NOT appear on camera/admin feeds (client-side UI object, but technically created as GameObject)

#### Body Eating Ability (Lines 1446-1479, Buttons.cs)
**Trigger:** Player presses button (vulture is within MaxReportDistance of an unreported dead body)

**Sequence:**
1. **Find targetable bodies** (Lines 1449-1470):
   - Use `Physics2D.OverlapCircleAll()` with `PlayerControl.MaxReportDistance` radius
   - Filter to objects with tag "DeadBody" (Line 1450)
   - For each dead body found:
     - Must have `DeadBody` component (Line 1451)
     - Must NOT be reported: `!component.Reported` (Line 1452)
     - Check line-of-sight: `!PhysicsHelpers.AnythingBetween(truePosition, truePosition2, Constants.ShipAndObjectsMask, false)` (Line 1455)
     - Check distance: `Vector2.Distance(truePosition2, truePosition) <= MaxReportDistance` (Line 1455)
     - Player must be able to move: `PlayerControl.LocalPlayer.CanMove` (Line 1455)

2. **Eat first valid body** (Lines 1456-1465):
   - Get parent player ID from `GameData.Instance.GetPlayerById(component.ParentId)` (Line 1456)
   - Send `CustomRPC.CleanBody` RPC with `(playerInfo.PlayerId, Vulture.vulture.PlayerId)` (Lines 1458-1461)
   - Call `RPCProcedure.cleanBody(playerInfo.PlayerId, Vulture.vulture.PlayerId)` locally (Line 1462)
   - Reset cooldown: `Vulture.cooldown = vultureEatButton.Timer = vultureEatButton.MaxTimer` (Line 1464)
   - Play sound "vultureEat" (Line 1465)
   - Break loop (eat only one body per button press) (Line 1466)

**Button Cooldown (Lines 1464):**
- Resets to `vultureEatButton.MaxTimer` which is initialized to `Vulture.cooldown` (Line 132)
- Default cooldown: 15 seconds (not cumulative like Eraser)

#### CleanBody RPC (RPC.cs:527-545)
**Payload:** `byte playerId, byte cleaningPlayerId`

**Handler:** `cleanBody(byte playerId, byte cleaningPlayerId)`
- **Medium interaction** (Lines 528-531):
  - If Medium has dead body tracked, mark it as `wasCleaned = true` (prevents Medium from using this body later)
  
- **Destroy dead body object** (Lines 533-538):
  - Find all `DeadBody` objects
  - If body's parent player ID matches `playerId`, destroy the GameObject
  - This removes the body from the map

- **Vulture eat count** (Lines 539-544):
  - Check if `cleaningPlayerId == Vulture.vulture.PlayerId`
  - If yes:
    - Increment `Vulture.eatenBodies++`
    - Check if `Vulture.eatenBodies == Vulture.vultureNumberToWin`
    - If yes, set `Vulture.triggerVultureWin = true` (signals game-end check)

#### Vulture Win Condition (EndGamePatch.cs:445-452, CheckAndEndGameForVultureWin)
**Trigger:** Called every frame from `ShipStatus.CheckEndGameConditions()` (Line 407)

**Logic (Lines 445-452):**
```
if (Vulture.triggerVultureWin) {
    GameManager.Instance.RpcEndGame((GameOverReason)CustomGameOverReason.VultureWin, false);
    return true;
}
```

- **If `triggerVultureWin == true`**:
  - Send RPC to end game with custom game-over reason `VultureWin` (Line 448)
  - Game ends immediately (not at meeting end; during normal gameplay)
  - Vulture is marked as winner, all other players as losers (Line 149, EndGamePatch.cs)

**Win screen display (EndGamePatch.cs:299-300):**
- Text: "Vulture Wins"
- Color: Vulture.color (brown)

### Options Table

| Option Name | Option ID | Type | Default | Min | Max | Step |
|---|---|---|---|---|---|---|
| Vulture Spawn Rate | 340 | Dropdown | - | - | - | - |
| Vulture Cooldown | 341 | Float | 15.0s | 10.0s | 60.0s | 2.5s |
| Number Of Corpses Needed To Be Eaten | 342 | Float | 4 | 1 | 10 | 1 |
| Vulture Can Use Vents | 343 | Bool | true | - | - | - |
| Show Arrows Pointing Towards The Corpses | 344 | Bool | true | - | - | - |

**Option Semantics:**
- **Vulture Cooldown:** Time between eating bodies (not cumulative, resets on each eat)
- **Number Of Corpses Needed To Be Eaten:** Win threshold; can be 1-10
- **Vulture Can Use Vents:** If true, vulture can use impostor vents (neutral role doesn't have vent access by default in vanilla)
- **Show Arrows Pointing Towards The Corpses:** If false, no arrows shown even if bodies exist

### Networking (RPC)

#### RPC Enum Values
- `CleanBody = 110` (CustomRPC enum, RPC.cs:110)

#### CleanBody (RPC.cs:527-545)
Already documented above in "Body Eating Ability"

### Edge Cases in Code

1. **Already Reported Bodies:**
   - If body is already reported by another player, Vulture cannot eat it (Line 1452: `!component.Reported` check)
   - Reported bodies remain on map but cannot be eaten

2. **Line-of-Sight Blocking:**
   - If obstacle between vulture and body, cannot eat (Line 1455: `PhysicsHelpers.AnythingBetween()`)
   - This includes walls, objects, other players
   - Vent exit does not bypass line-of-sight check

3. **Vulture Dies Before Winning:**
   - If vulture dies and eaten count < threshold, vulture is removed from winners (EndGamePatch.cs:98)
   - Remaining players continue game (no Vulture win possible if vulture is dead)
   - `triggerVultureWin` is NOT checked if vulture is dead (game-end logic doesn't skip, but vulture can't win if not in winner list)

4. **Multiple Vultures (if multiple roles allowed):**
   - Each vulture increments their own eat count via `cleaningPlayerId` check (Line 539)
   - Multiple vultures can each trigger separate win conditions
   - Each vulture independently counts to their own win threshold

5. **Janitor Cleaning Bodies:**
   - Janitor (impostor role) has `cleanAllDeadBodies()` ability
   - When Janitor cleans ALL bodies, `cleanBody` RPC is called for each body
   - If Vulture triggered the clean (not Janitor), `cleaningPlayerId` would be Vulture, not Janitor
   - If Janitor cleans, `cleaningPlayerId == Janitor.janitor`, so Vulture eat count NOT incremented (only increments if Vulture cleans via button)

6. **Vulture + Medium Interaction:**
   - Eaten body is marked `wasCleaned = true` in Medium's `futureDeadBodies` list (Line 530)
   - This prevents Medium from using eaten bodies in séances
   - Medium cannot ask eaten bodies' souls

7. **Vulture Win During Meeting:**
   - If Vulture reaches win threshold during meeting (e.g., body eaten just before meeting called):
     - `triggerVultureWin` is set, but game hasn't checked yet
     - Meeting proceeds normally
     - After meeting ends, game checks `triggerVultureWin` and ends game (Vulture wins)
   - If Vulture reaches threshold mid-meeting (unlikely), meeting continues but game ends when checking

8. **Bodies Cleaned by Vanilla Meeting Mechanics:**
   - At meeting end, dead bodies are NOT automatically cleaned/removed in vanilla
   - Bodies persist unless Vulture eats them or Janitor cleans them
   - Vulture can continue eating bodies across multiple meetings

9. **Vulture + Vents:**
   - If `canUseVents = true`, Vulture can enter vents like impostor
   - While venting, Vulture can still eat nearby bodies if line-of-sight allows (venting doesn't block check)
   - Arrow updates continue during venting (arrows show dead body positions)

10. **Vulture + Impostor Vision (if mod supports it):**
    - TOR does not have an option for Vulture impostor vision
    - Vulture sees map as crewmate would (no impostor vision enhancement)

11. **Vulture Erased:**
    - If Vulture is erased by Eraser, `Vulture.clearAndReload()` is called (RPC.cs:787)
    - Eaten bodies count resets to 0
    - `triggerVultureWin` is reset to false
    - Vulture is no longer the vulture; becomes crewmate (via erasePlayerRoles)

12. **Arrow Creation During Gameplay:**
    - Arrows are created fresh each frame if body count differs (arrowUpdate logic)
    - Old arrows are destroyed
    - No memory leak if bodies frequently added/removed

13. **Arrow Visibility on Cams/Admin:**
    - Arrows are client-side UI objects (created in `localArrows` list on vulture's client)
    - Arrows do NOT render on camera/admin feeds (unlike Medium's souls or Ninja's traces)
    - Only vulture sees arrows (per-client logic in PlayerControlPatch.cs:717-741)

14. **Vulture + Lovers:**
    - No special interaction documented
    - If Vulture is a lover and one lover dies, both die (Lovers mechanic independent)
    - Eating bodies doesn't affect Lovers mechanic

15. **Vulture + Guesser:**
    - No special interaction preventing Guesser from guessing Vulture
    - Guesser can guess Vulture as any role

### Assets

**Sprite Resources:**
- Eat button: `TheOtherRoles.Resources.VultureButton.png` (115f scale) — loaded in `Vulture.getButtonSprite()`
- Arrow: `TheOtherRoles.Resources.Arrow.png` (200f scale) — shared resource, loaded in `Arrow.getSprite()`

**Audio:**
- Eat sound: `"vultureEat"` → `assets/audio/vultureeat.ogg` (via SoundEffectsManager, loaded from ToRAudio assetbundle)

---

## TOU-Mira Mapping Sketch

### Eraser Role Porting Pattern

**Best Template Match:** Witch role (delayed execution at meeting end) + role-changing utility

#### MiraAPI/TOU-Mira Mapping

1. **Role State (extends `ImpostorRole`)**
   - `ITownOfUsRole` interface for auto-registration
   - Store `futureErased`, `alreadyErased`, `currentTarget` in role instance (not static)
   - Team: `ModdedRoleTeams.Impostor`
   - RoleAlignment: `RoleAlignment.ImpostorSupport` (support role, non-killing)

2. **Button Targeting (TownOfUsRoleButton<EraserRole>)**
   - `CanUse()`: Check `PlayerControl.LocalPlayer.CanMove && CurrentTarget != null`
   - `OnClick()`: Add to `futureErased` list, increment cooldown
   - Cooldown handling: Track cumulative cooldown in role state, update button MaxTimer each click
   - Use `setTarget()` pattern from HunterKillButton (MiscUtils.GetClosestLivingPlayer or similar)

3. **Role Erase at Meeting End (RegisterEvent + VotingCompleteEvent)**
   - Listen to `VotingCompleteEvent` from MiraAPI (called after vote before exile animation)
   - Query `futureErased` list of all active Eraser role instances
   - For each target:
     - Call `target.RpcRemoveRole()` or similar to strip role (check TOU-Mira for role removal RPC)
     - Add to `alreadyErased` list
   - Alternative: If no built-in role removal, use `target.RpcSetRole(RoleTypes.Crewmate)` to forcibly change to crewmate role

4. **Target Restrictions (searchable in reference/TOU-Mira)**
   - Check TOU-Mira's Lawyer/Prosecutor roles for "cannot target" patterns (they have target restrictions)
   - Jackal/Sidekick handling: Likely via `PlayerUtils` or role filtering
   - Spy handling: Check if TOU-Mira has Spy equivalent; if so, use its targeting restrictions

#### Known TOU-Mira Gotchas for Eraser
- **Role removal RPC**: TOU-Mira's roles are tightly bound to `PlayerData.Role`. Stripping via `clearAndReload()` equivalent requires either:
  - A custom RPC that removes the role object entirely
  - Setting player role to `RoleTypes.Crewmate` via `SetRole` RPC
  - Check `CustomRoleUtils.RemoveRoleOfType<EraserRole>(player)` (if exists)
- **Cumulative cooldown**: Button cooldown increases per use; track in role state (not button state) to persist across button resets
- **Modifier handling**: Check if TOU-Mira's `RemoveRole` also clears modifiers (default in TOR is to NOT clear modifiers)

### Vulture Role Porting Pattern

**Best Template Match:** Glitch role (neutral win condition) + body-eating state tracking

#### MiraAPI/TOU-Mira Mapping

1. **Role State (extends `NeutralRole`)**
   - `ITownOfUsRole` interface with `WinConditionMet()` method
   - Store `eatenBodies` count, `vultureNumberToWin` threshold, `localArrows` list
   - Team: `ModdedRoleTeams.Custom` (Neutral Scavenger team, if available) or `ModdedRoleTeams.Neutral`
   - RoleAlignment: `RoleAlignment.NeutralScavenger` or equivalent
   - `canUseVents`: Check TOU-Mira's vent handling for neutral roles (may require custom modifier or permission check)

2. **Body Detection & Eating (Custom button + Collider2D/Physics2D pattern)**
   - TOU-Mira likely has body-detection patterns in Medic/Tracker roles
   - Search reference/TOU-Mira for:
     - `Physics2D.OverlapCircleAll()` usage (body detection)
     - `DeadBody` GameObject interaction
     - `MiscUtils.GetBodiesInRange()` (if it exists)
   - Eating mechanic: Custom RPC `RpcEatBody(bodyId)` or iterate found bodies and send per-body RPC

3. **Dead Body Removal (Janitor pattern)**
   - Reference TOU-Mira's Janitor role for body cleanup RPC:
     - Check `JanitorRole.cs` or `Janitor`*Button for body destruction
     - Likely uses `DeadBody.gameObject.DeepDestroy()` or similar
     - May trigger Medium's cleanup flag (search for `wasCleaned`)

4. **Win Condition (Glitch/GlitchRole pattern)**
   - Implement `bool WinConditionMet()` method in EraserRole
   - Check: `eatenBodies >= vultureNumberToWin`
   - Called by TOU-Mira's game-end logic every frame
   - TOU-Mira will handle win announcement and game-over screen

5. **Arrow Rendering (ArrowTargetModifier or custom)**
   - For arrows to dead bodies (not single target):
   - Option A: Use `ArrowTargetModifier` for each body (overkill if many bodies)
   - Option B: Custom arrow management in role's `FixedUpdate()` (mirror PlayerControlPatch logic)
   - Arrow updates sync with `DeadBody[]` array each frame
   - Check TOU-Mira's `Arrow` class equivalents (reference/TOU-Mira for arrow patterns)

#### Known TOU-Mira Gotchas for Vulture
- **Neutral role vent access**: Check if TOU-Mira's neutral roles can vent by default. If not, may need custom modifier to grant vent permission
- **Win condition timing**: TOU-Mira's game-end check may differ from TOR's per-frame trigger. Test when `WinConditionMet()` is evaluated (likely frame-based)
- **Body destruction**: Ensure body destruction is synchronized across clients via RPC (not local-only)
- **Arrow layer and visibility**: Confirm arrows don't render on admin/cams (set layer appropriately or check Visible flag)

---

## Summary of Implementation Quirks & Trickiest Details for Porters

### Eraser

1. **Cumulative Cooldown Mechanic:** Unlike most roles, cooldown increases by 10s **each use**, not reset. This requires:
   - Tracking cooldown state in role, not button
   - Incrementing `button.MaxTimer` on each press
   - Persisting across button resets (no auto-reset at meetings)

2. **Role Stripping at Meeting End:** Erasing doesn't kill the player; it removes their role entirely. Porting requires:
   - A role-removal RPC or equivalent in TOU-Mira/MiraAPI
   - Handling impostor-to-crewmate conversion (legal and balanced)
   - Ensuring tasks behave sensibly after role strip (crewmate tasks appear if impostor erased)

3. **Modifiers NOT Cleared (Default):** If erased player has Lovers/Mini/other modifiers, they remain active after erase. This can create unusual states:
   - Erased impostor becomes crewmate but retains Mini modifier (still small)
   - Erased lover still linked to other lover (both crewmates now, both still loved)

4. **Target Restrictions:** Jackal/Sidekick/formerJackals cannot be erased (check in `canBeErased()` equivalent). Porting requires:
   - Proper check against these role types
   - Gracefully handling erase failure (no error, no cooldown consumed)

5. **Already-Erased Tracking:** `alreadyErased` list prevents Guesser from re-guessing erased targets. This is role-specific (only Eraser's own erased targets). Ensure this interacts correctly with other guesser roles if present in addon.

### Vulture

1. **Immediate Win Condition:** Vulture's win triggers instantly when body count threshold reached (not at meeting end like Witch). Game-end check runs every frame; ensure `WinConditionMet()` or equivalent is checked frequently enough.

2. **Body Synchronization:** Arrows update every frame based on current `DeadBody` array. If bodies are added/destroyed frequently (e.g., Janitor actively cleaning), arrow list must be re-synced correctly. Inefficient if many bodies; consider caching.

3. **Eating vs. Cleaning Distinction:** Only Vulture's direct button press increments eat count. If Janitor cleans a body, it's destroyed but eat count doesn't increment. Porting must ensure:
   - Only Vulture's own `cleanBody` RPC (or equivalent) increments count
   - Check `cleaningPlayerId` to distinguish Vulture from other cleaners

4. **Line-of-Sight Check:** Bodies must have line-of-sight (no obstacle) for Vulture to eat. This uses `PhysicsHelpers.AnythingBetween()`. Porting should replicate this; if TOU-Mira has equivalent, use it (check `MiscUtils` for raycast patterns).

5. **Neutral Role Vent Access:** TOR allows Vulture (a neutral) to vent if `canUseVents = true`. This is unusual for neutrals. Porting may need:
   - Custom modifier to grant vent permission
   - Or check if TOU-Mira's neutral roles have built-in vent options
   - Verify vent access doesn't break win condition or other mechanics

6. **Medium Interaction:** Eaten bodies are marked `wasCleaned` in Medium's tracking list. Porting must ensure:
   - Medium role (if present) is notified of eaten body
   - Medium cannot use eaten body in séances
   - Search reference/TOU-Mira for Medium role to understand integration point

7. **Arrow Persistence:** Arrows are created/destroyed every frame based on body array. This is not optimized (creates/destroys arrows frequently). Consider:
   - Caching arrow list more efficiently
   - Only updating arrows that changed
   - Destroying arrows only when bodies actually disappear (not every frame)

---

## Code Path Summary for Quick Reference

### Eraser Critical Files
- State: `TheOtherRoles.cs:969-995`
- Options: `CustomOptionHolder.cs:481-483`
- Button: `Buttons.cs:1100-1119`
- Targeting: `PlayerControlPatch.cs:275-283`
- Erase execution: `ExileControllerPatch.cs:36-46`
- Role erasure: `RPC.cs:733-807` (erasePlayerRoles)
- RPC mark: `RPC.cs:809-816` (setFutureErased)
- Guesser restriction: `MeetingPatch.cs:621`
- Target check: `Helpers.cs:228-230` (canBeErased)

### Vulture Critical Files
- State: `TheOtherRoles.cs:1347-1379`
- Options: `CustomOptionHolder.cs:568-572`
- Button: `Buttons.cs:1446-1479`
- Arrow updates: `PlayerControlPatch.cs:717-741` (vultureArrowsUpdate)
- Body eating: `RPC.cs:527-545` (cleanBody)
- Win trigger: `EndGamePatch.cs:445-452` (CheckAndEndGameForVultureWin)
- Win check: `EndGamePatch.cs:407` (called from ShipStatus.CheckEndGameConditions)
- Win display: `EndGamePatch.cs:299-300`
