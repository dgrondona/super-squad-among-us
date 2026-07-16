# TheOtherRoles Mafia Roles Documentation: Godfather, Mafioso, Janitor

## Open Questions for Porting Decision

1. **Should Mafioso exist without Godfather?** In TOR, Mafioso cannot kill if Godfather is alive, creating a dependency. Should the port allow spawning just Mafioso, or enforce the trio requirement? (TOR requires 3+ impostors to spawn the trio at all.)

2. **Should we port Janitor?** User explicitly requested Godfather + Mafioso only. Janitor is a helper role that only removes bodies and shares the Mafia team. If porting solo Godfather/Mafioso without Janitor, we need to verify whether the team assignment logic breaks (likely not, since it's just a spawn check).

3. **Are Godfather/Mafioso just recolored Impostors with one gimmick?** They use vanilla kill mechanics entirely, except Mafioso's conditional disable. Confirm: should they be roles or modifiers on top of Impostor?

4. **Kill cooldown reset on Godfather death?** TOR does NOT reset Mafioso's kill cooldown when Godfather dies — only the button visibility changes. Confirm this is desired behavior or should cooldown reset occur.

5. **Team visibility in intro?** TOR does NOT show Mafia team in intro cutscene (unlike Lovers or other paired roles). Confirm whether port should follow this or add intro reveal.

---

## Mafia Trio Concept

### How They Work Together

**Files:**
- Role definitions: `TheOtherRoles.cs:216-252`
- Assignment: `Patches/RoleAssignmentPatch.cs:173-181`
- UI disable logic: `Patches/UpdatePatch.cs:176-189, 296-298`
- Sabotage block: `Patches/UsablesPatch.cs:209-210`

**Trio Assignment (RoleAssignmentPatch.cs:173-181):**
```
if (data.impostors.Count >= 3 
    && data.maxImpostorRoles >= 3 
    && rnd.Next(1, 101) <= CustomOptionHolder.mafiaSpawnRate.getSelection() * 10)
{
    setRoleToRandomPlayer((byte)RoleId.Godfather, data.impostors);
    setRoleToRandomPlayer((byte)RoleId.Janitor, data.impostors);
    setRoleToRandomPlayer((byte)RoleId.Mafioso, data.impostors);
    data.maxImpostorRoles -= 3;
}
```

**Requirements:**
- At least 3 impostors in the game
- `maxImpostorRoles >= 3` (role slots available for all three)
- Mafia spawn rate check (0-100%, configurable in 10% increments)
- When all conditions met, assigns **exactly one player per role** (no duplicates)
- Consumes 3 impostor role slots

**Order of assignment:** Godfather → Janitor → Mafioso (sequential from impostor list)

**Can be broken by:** Picking Godfather solo? No. In TOR, they are only assigned as a trio. However, code doesn't explicitly prevent spawning just one if bypass methods used.

---

## Godfather Role

### Role State

**Files and Locations:**
- State: `TheOtherRoles.cs:216-223` (static class `Godfather`)

**Fields:**
- `godfather`: `PlayerControl` - the godfather player instance (null when not assigned)
- `color`: `Palette.ImpostorRed` - team color (same as vanilla impostor)

**Lifecycle:**
- `clearAndReload()` called at game reset: sets `godfather = null`
- Role assignment in RPC.cs:290: `Godfather.godfather = player;`

### Abilities

**Primary ability:** Vanilla impostor kill button
- No custom kill button sprite; uses vanilla red kill button
- No custom cooldown option; uses vanilla impostor kill cooldown (default 25s, set globally in game options)
- Can target any player (crewmate or impostor)
- No range restrictions beyond vanilla killDistance

**Secondary ability:** Vanilla sabotage
- Uses vanilla sabotage panels and cooldown
- No restrictions (unlike Mafioso)

**Team leadership indicator:**
- Other team members (Mafioso, Janitor) see Godfather labeled as "(G)" in-game (UpdatePatch.cs:176-189)
- Godfather sees his team members labeled as "(M)" for Mafioso and "(J)" for Janitor

### Options

**Mafia spawn rate** (CustomOptionHolder.cs:465)
- Option ID: 18
- Type: Dropdown (rates array)
- Default: varies by TOR setting
- Parent option: none (top-level)
- **Note:** This is the ONLY Mafia-level option. Godfather and Mafioso have no individual options.
- Janitor has sub-option: Janitor Cooldown

### Networking (RPC)

**Role assignment (RPC.cs:289-290):**
```csharp
case RoleId.Godfather:
    Godfather.godfather = player;
    break;
```
- Triggered on all clients when role is assigned during game start

**Death cleanup (RPC.cs:761):**
```csharp
if (player == Godfather.godfather) Godfather.clearAndReload();
```
- When Godfather dies, `Godfather.godfather` is set to null
- This triggers UI update in UpdatePatch, enabling Mafioso kill button

**Thief stealing (RPC.cs:1114):**
```csharp
if (target == Godfather.godfather) Godfather.godfather = thief;
```
- If Thief kills Godfather, Thief role becomes Godfather

### Edge Cases & Interactions

1. **Godfather death (voted out or killed):** When `Godfather.godfather.Data.IsDead` becomes true, Mafioso's kill button immediately re-enables. Godfather's role reference is NOT cleared until end-of-game cleanup. Mafioso's kill cooldown is NOT reset on Godfather's death.

2. **Lovers:** No special handling. If Godfather's lover is exiled, Godfather dies via Lovers mechanic (standard). No special Godfather-Lover interaction in code.

3. **Thief:** If Thief kills Godfather, Thief's role becomes Godfather. Mafioso still sees Godfather as leader (by PlayerId match), but now it's the Thief playing as Godfather.

4. **Win condition:** Standard impostor win (all crewmates dead). No special Godfather-only win conditions.

5. **Camera/Admin table:** Appears as Impostor; no special visual treatment.

---

## Mafioso Role

### Role State

**Files and Locations:**
- State: `TheOtherRoles.cs:225-232` (static class `Mafioso`)

**Fields:**
- `mafioso`: `PlayerControl` - the mafioso player instance (null when not assigned)
- `color`: `Palette.ImpostorRed` - team color

**Lifecycle:**
- `clearAndReload()` called at game reset: sets `mafioso = null`
- Role assignment in RPC.cs:292-293: `Mafioso.mafioso = player;`

### Abilities

**Primary ability: Kill button (conditional)**
- Uses vanilla kill button, BUT button is **hidden (disabled)** if Godfather is alive

**Kill button visibility logic (UpdatePatch.cs:296-302):**
```csharp
else if (Mafioso.mafioso != null && Mafioso.mafioso == PlayerControl.LocalPlayer 
         && Godfather.godfather != null && !Godfather.godfather.Data.IsDead)
    enabled = false;
...
if (enabled) __instance.KillButton.Show();
else __instance.KillButton.Hide();
```

**When Godfather is ALIVE:**
- KillButton is hidden (`KillButton.Hide()`)
- Mafioso cannot kill any target
- Attempting to use vanilla kill button does nothing (button not visible)

**When Godfather is DEAD or NULL:**
- KillButton is shown (`KillButton.Show()`)
- Mafioso can kill like a normal impostor
- Kill cooldown is NOT automatically reset when Godfather dies; the existing cooldown timer persists

**Sabotage block (UsablesPatch.cs:209-210):**
```csharp
bool blockSabotageMafioso = (Mafioso.mafioso != null && Mafioso.mafioso == PlayerControl.LocalPlayer 
                             && Godfather.godfather != null && !Godfather.godfather.Data.IsDead);
```
- When Godfather is alive, Mafioso cannot use sabotage (sabotage panels are disabled)
- This is an additional restriction beyond just the kill button

**Team role indicator:**
- Sees Godfather labeled as "(G)" and Janitor labeled as "(J)" in-game

### Options

**None.** Mafioso has no custom options. Uses vanilla impostor kill cooldown and sabotage cooldowns set globally.

### Networking (RPC)

**Role assignment (RPC.cs:292-293):**
```csharp
case RoleId.Mafioso:
    Mafioso.mafioso = player;
    break;
```

**Death cleanup (RPC.cs:762):**
```csharp
if (player == Mafioso.mafioso) Mafioso.clearAndReload();
```

**Thief stealing (RPC.cs:1115):**
```csharp
if (target == Mafioso.mafioso) Mafioso.mafioso = thief;
```

**No custom kill RPC:** Uses vanilla `MurderPlayerRpc` when able to kill.

### The Core Gimmick: Conditional Kill Gate

**TOR's design philosophy:** Mafioso is a "sidekick" impostor who cannot act independently. The kill button state machine is entirely **local-side** in the UI update loop (UpdatePatch).

**When promoted (Godfather dies):**
1. Godfather's death RPC clears `Godfather.godfather` (or marks `IsDead`)
2. Next `FixedUpdate` in UpdatePatch, the check `Godfather.godfather != null && !Godfather.godfather.Data.IsDead` returns **false**
3. `enabled = false` condition is skipped, button shows
4. Mafioso can immediately click kill button
5. Kill cooldown remains at whatever value it was (usually maxed out from not being able to kill earlier)

**No "promotion animation" or role swap:** Mafioso doesn't change roles or get new title; the button state simply changes.

### Edge Cases & Interactions

1. **Godfather death while Mafioso is targeting:** No change to Mafioso's ability to click kill mid-targeting. The button becomes clickable immediately; no in-flight kill is interrupted.

2. **Lover of Mafioso:** If Mafioso's lover dies/is exiled, Mafioso dies via Lovers mechanic (standard). Godfather death has no special interaction with Lovers.

3. **Thief stealing Mafioso:** Thief becomes Mafioso. The UI check now applies to the Thief-as-Mafioso player. If Thief steals Godfather first, the new Godfather is active and new Mafioso-thief can kill.

4. **Meeting during Godfather death:** If Godfather is voted out in a meeting, the death is resolved, and on the next game tick post-meeting, Mafioso's button becomes available.

5. **Meeting during Mafioso button enabled state:** Mafioso can vote and talk; no special restrictions.

6. **Camera/Admin table:** Mafioso appears as Impostor. Impostor icons do not differentiate between Godfather/Mafioso/Janitor.

---

## Janitor Role (OUT OF SCOPE FOR THIS PORT)

### Summary

**User explicitly requested Godfather + Mafioso only. Janitor documented here for completeness and to identify any trio-dependency issues.**

**Files and Locations:**
- State: `TheOtherRoles.cs:235-252`
- Button: `Buttons.cs:354-390`
- Options: `CustomOptionHolder.cs:465-466`
- RPC: `RPC.cs:110, 527-541`

**Ability: Clean dead bodies**
- Button: Removes dead body GameObjects within `MaxReportDistance`
- Cooldown: `CustomOptionHolder.janitorCooldown` (default 30f, range 10-60s, step 2.5s)
- Cannot use vanilla kill button (hidden like Mafioso when alive, but always hidden for Janitor)

**Team role:** Part of Mafia trio, sees and is seen by Godfather and Mafioso with labels

**Key note:** If porting only Godfather + Mafioso without Janitor, the RoleAssignmentPatch assignment order (Godfather → Janitor → Mafioso) will skip Janitor. The trio requirement logic (`data.impostors.Count >= 3`) still holds; you'd need to modify assignment to skip Janitor or keep the trio requirement.

---

## Win Conditions & Death

### Win Condition

**Standard impostor win:** All living crewmates are dead. No special Mafia-only win condition.

- Godfather death: Mafia continues playing (Mafioso gains kill ability)
- Mafioso death: Godfather continues (Janitor continues)
- Janitor death: Godfather + Mafioso continue (no cleanup ability left, but they can still kill)

### Voting/Exile Logic

**No special vote save mechanic** (unlike Witch's "voting witch saves targets"). If Godfather is voted out, Godfather dies and is removed from play. If Mafioso is voted out, Mafioso dies.

**Meeting name labels (UpdatePatch.cs:183-189):**
```csharp
if (MeetingHud.Instance != null)
    foreach (PlayerVoteArea player in MeetingHud.Instance.playerStates)
        if (Godfather.godfather != null && Godfather.godfather.PlayerId == player.TargetPlayerId)
            player.NameText.text = Godfather.godfather.Data.PlayerName + " (G)";
        else if (Mafioso.mafioso != null && Mafioso.mafioso.PlayerId == player.TargetPlayerId)
            player.NameText.text = Mafioso.mafioso.Data.PlayerName + " (M)";
        else if (Janitor.janitor != null && Janitor.janitor.PlayerId == player.TargetPlayerId)
            player.NameText.text = Janitor.janitor.Data.PlayerName + " (J)";
```
- Team members see each other labeled in the vote area
- Non-Mafia players see no special labels

---

## Options Table

| Option Name | Option ID | Type | Default | Min | Max | Step | Parent |
|---|---|---|---|---|---|---|---|
| Mafia Spawn Rate | 18 | Dropdown | ? | 0% | 100% | 10% | (root) |
| Janitor Cooldown | 19 | Float | 30.0s | 10.0s | 60.0s | 2.5s | Mafia Spawn Rate |

**Godfather options:** None (uses vanilla impostor kill cooldown)
**Mafioso options:** None (uses vanilla impostor kill cooldown)

---

## Networking (RPC) Summary

### RPC Values

| RPC Name | Value | Payload |
|---|---|---|
| CleanBody | 110 | `byte playerId, byte cleaningPlayerId` |

### RPC Handlers

#### Role Assignment (RPC.cs:289-296)
All three roles assigned via standard role RPC:
- Payload: `byte roleId, byte playerId`
- Handler: Sets static class reference (`Godfather.godfather = player`, etc.)

#### Death Cleanup (RPC.cs:761-762)
When player dies:
```csharp
if (player == Godfather.godfather) Godfather.clearAndReload();
if (player == Mafioso.mafioso) Mafioso.clearAndReload();
```

#### Thief Steal (RPC.cs:1114-1115)
When Thief kills a Mafia member:
```csharp
if (target == Godfather.godfather) Godfather.godfather = thief;
if (target == Mafioso.mafioso) Mafioso.mafioso = thief;
```

### No Custom Kill RPC

Godfather and Mafioso use vanilla `MurderPlayerRpc` for kills. No custom RPC for enabling/disabling Mafioso's kill button (all handled locally in UpdatePatch UI logic).

---

## Sprites & Audio

### Sprite Resources

| Asset | File | Scale | Usage |
|---|---|---|---|
| Janitor Clean Button | `TheOtherRoles.Resources.CleanButton.png` | 115f | Janitor button UI |
| Godfather Kill Button | (vanilla) | N/A | Standard impostor red kill button |
| Mafioso Kill Button | (vanilla) | N/A | Standard impostor red kill button (same as Godfather) |

### Audio

| Sound | Usage |
|---|---|
| `cleanerClean` | Janitor cleans a body (Buttons.cs:375) |
| (vanilla kill sound) | Godfather/Mafioso kill (standard impostor) |

### Intro Cutscene

**No special Mafia intro reveal.** Unlike Lovers or Arsonist, the Mafia does not show team members during the intro cutscene. Team members discover each other during gameplay via name labels.

---

## Porting to TOU-Mira/MiraAPI: Mapping Sketch

### Role Structure

**Best template match:** `Swooper` role (standard impostor with custom ability)

**MiraAPI mapping:**
- Godfather, Mafioso, Janitor: Extend `ImpostorRole` (or `RoleBehaviour` with role base)
- Implement `ITownOfUsRole` for standard role UI/name/description
- Godfather: No modifiers needed (vanilla kill only)
- Mafioso: Use modifier + button `CanUse()` check to gate kill ability when Godfather alive
- Janitor: Use custom button for clean ability (similar to MiraAPI button pattern)

### Kill Button Gate (Mafioso's Core Mechanic)

**Option A: Modifier-based**
```csharp
// Pseudo-code
public class MafiasoKillGateModifier : BaseModifier
{
    public override bool CanUseKill => Godfather.godfather != null && !Godfather.godfather.HasDied();
}

// On MafiasoRole button:
public override bool CanUse()
{
    if (!base.CanUse()) return false;
    if (Player.HasModifier<MafiasoKillGateModifier>() && !CanUseKill) return false;
    return true;
}
```

**Option B: Button CanUse() check**
```csharp
public sealed class MafiasoKillButton : TownOfUsRoleButton<MafiasoRole>
{
    public override bool CanUse()
    {
        if (!base.CanUse()) return false;
        
        // Mafioso can only use kill if Godfather is dead or null
        if (Godfather.godfather != null && !Godfather.godfather.HasDied())
            return false;
        
        return true;
    }
}
```

**Option B is simpler** and mirrors TOR's local UI logic. No need for a modifier; just check in `CanUse()`.

### Sabotage Block (Mafioso)

**MiraAPI may not have sabotage-level granularity.** Check if TOU-Mira overrides sabotage button state or if vanilla sabotage is just allowed/blocked globally. If Mafioso needs to block sabotage when Godfather alive, may need patch in `UsablesPatch` equivalent or a modifier that disables sabotage button.

### Team Visibility (Name Labels)

**Existing pattern:** Lovers role shows team members with suffix in `UpdatePatch.cs` (vanilla patch).

**For Mafia:** Port the label logic from UpdatePatch.cs:176-189:
- During `HudUpdate`, check if local player is any Mafia role
- If Godfather, iterate players and append "(G)" to Godfather, "(M)" to Mafioso, "(J)" to Janitor
- Similarly in MeetingHud

**MiraAPI integration:** If TOU-Mira has a centralized role display patch, hook there. Otherwise, create a role-specific patch.

### Team Assignment Logic

**RoleAssignmentPatch.cs:173-181 equivalent:**
- In TOU-Mira's role assignment phase, check if 3+ impostors and mafia option enabled
- Call assignment on three separate impostor players (likely via `RoleManager` or MiraAPI's role assignment API)
- Consume 3 role slots

**TOU-Mira precedent:** Check if TOU-Mira has "linked role" assignment (e.g., does Egotist have a partner role that must spawn together?). If yes, follow that pattern. If no, implement sequential role assignment from impostor list.

### Options

**One spawn rate option for the trio** (no individual role options for Godfather/Mafioso, one sub-option for Janitor).

```csharp
public sealed class MafiaOptions : AbstractOptionGroup<MafiaRole>
{
    [ModdedNumberOption]
    public NumberOption MafiaSpawnRate { get; } = new(this, "Mafia Spawn Rate", 30f, 0f, 100f, 10f);
    
    [ModdedNumberOption]
    public NumberOption JanitorCooldown { get; } = new(this, "Janitor Cooldown", 30f, 10f, 60f, 2.5f);
}
```

### RPC Considerations

**Godfather/Mafioso use vanilla kill RPC** (no custom RPC needed for kills).

**Janitor clean RPC** (CustomRPC.CleanBody):
- Payload: `byte deadPlayerId, byte janitorPlayerId`
- Handler: Destroy DeadBody GameObject, mark Medium tracking if needed

**No custom RPC for kill button enable/disable** (handled locally in UI logic).

### Win Condition

**Standard impostor win** (all crewmates dead). Implement via vanilla game-over check or TOU-Mira's `CheckWinCondition` equivalent.

---

## Critical Implementation Details for Porter

### 1. Godfather is the "Leader"

In TOR, Godfather has no special power beyond vanilla kill. The gimmick is **Mafioso's restriction**. Godfather is essentially a regular impostor whose presence gates another player's abilities. Design Godfather as a standard impostor role; no custom button needed.

### 2. Mafioso's Kill Button is Local-Only Logic

The `KillButton.Hide()` is called every `FixedUpdate` in `UpdatePatch`. It's **not** synced via RPC or modifier state; it's purely a UI decision based on local state checks. When porting:
- Use `CanUse()` return false OR hide button in `FixedUpdate` (mirror TOR's approach)
- Do NOT add RPC to sync button state

### 3. Kill Cooldown is NOT Reset on Promotion

When Godfather dies, Mafioso's kill cooldown does **not** reset. The timer continues from wherever it was. If you want a reset, add it as an explicit feature (not in TOR).

### 4. No Intro Cutscene Reveal

Unlike Lovers or Arsonist, Mafia team members do NOT see each other in intro. They discover team via name labels during gameplay and in meetings. If port adds intro reveal, it's a new feature beyond TOR.

### 5. Always Assign as Trio (or Document the Exception)

TOR's `RoleAssignmentPatch` requires all three to spawn together. If porting only Godfather + Mafioso, document this as a design change and handle the assignment logic accordingly (either keep require Janitor, or make Janitor optional).

### 6. Loves Interaction is Vanilla

No special Lovers mechanic for Mafia. If Godfather/Mafioso has a lover, follow standard Lovers rules (both die when one exiled). No special saves or promotions.

---

## File Path Reference

**TOR Source Files:**

| Purpose | File | Line Range |
|---|---|---|
| Role state | `TheOtherRoles/TheOtherRoles.cs` | 216-252 |
| Options | `CustomOptionHolder.cs` | 465-466 |
| Assignment | `Patches/RoleAssignmentPatch.cs` | 173-181 |
| UI / Kill button | `Patches/UpdatePatch.cs` | 176-189, 296-302 |
| Sabotage block | `Patches/UsablesPatch.cs` | 209-210 |
| Name labels in meeting | `Patches/MeetingPatch.cs` | 425 |
| Buttons | `Buttons.cs` | 354-390 (Janitor), vanilla kill for others |
| RPC assignment | `RPC.cs` | 289-296 |
| RPC death cleanup | `RPC.cs` | 761-762 |
| RPC thief steal | `RPC.cs` | 1114-1115 |
| RPC clean body | `RPC.cs` | 110, 527-541 |
| Role info / descriptions | `RoleInfo.cs` | 43-45 |

---

## Strengths of TOR's Design (For Porting)

1. **Simple state:** Godfather and Mafioso are minimal (just player reference + color). No complex timers or tracking.
2. **No sync issues:** All state checks are local (kill button gate is UI-only). No netcode required for most mechanics.
3. **Vanilla kill reuse:** Both use standard impostor kill mechanics; no custom kill button logic needed.
4. **Team labels are text-only:** No extra sprites or modifiers; just name text manipulation in existing patches.

## Challenges for Porting

1. **Kill button gate is UpdatePatch dependent:** TOU-Mira may have a different button management system. Need to find equivalent place to add Mafioso's `CanUse()` check or button hide logic.
2. **Trio assignment is hard-coded:** TOR checks for exactly 3 impostors to trigger Mafia. TOU-Mira's role assignment may not support trio-locking out-of-the-box; may need custom logic.
3. **Sabotage blocking:** If TOU-Mira doesn't expose sabotage button state per-role, may need a separate patch or modifier.
4. **Name label patching:** Lovers already show labels; piggyback on that system or create separate patch for Mafia labels.

---

## Summary: What to Port First

1. **Godfather role** (easiest) — vanilla impostor with no custom abilities
2. **Mafioso role** (medium) — add kill button gate check in `CanUse()` or UpdatePatch
3. **Janitor role** (if needed) — add clean button similar to other ability buttons
4. **Team label UI** (medium) — copy Lovers label pattern, adapt for three roles
5. **Trio assignment logic** (hard) — may require custom role assignment phase or wrapper

**Estimated complexity:** Medium-high (trio assignment is the blocker; core mechanics are straightforward).
