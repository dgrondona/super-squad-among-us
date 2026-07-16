# AllTheRoles Pelican, Witch, and Astral Role Implementation Notes

Research notes extracted from decompiled AllTheRoles source for porting these roles to another mod.

---

## PELICAN (Neutral Killing)

**Faction:** Neutral  
**Alignment:** NeutralKilling  
**Button Label:** `button.label.devour` (sprite: PelicanButton.png)  
**Color:** RGB(106, 21, 171)

### Ability Flow

**Devour Mechanic:**
- **Activation:** Single-target ability on enemy players. Target check (`n.a(A_1, e10)`) determines if target is valid (case `eh.b` = valid, case `eh.c` = blocked).
- **Effect on Devoured Player:**
  - Player's game object layer set to "Ghost" (invisible layer)
  - `Visible = false` (game property)
  - Player dies immediately with `val.Die((DeathReason)1, false)` (custom death reason — appears to be "Devoured" or similar)
  - Player added to `hk.d3` list (persistent list of devoured player IDs)
  - Player color set to `Color.clear` (transparent)
  - Player made non-moveable (`val.moveable = false`)
  - If local player: use/report buttons disabled, chat hidden, scanner disabled, name mask hidden
  - Notification sent: "You have been eaten by the Pelican!" (locale key `role.pelican.eaten.notify`)
  - Death logged via `e0.a(A_0, val, ey.h)` where `ey.h` is the custom death reason enum value

**Devoured Player Behavior:**
- Devoured players remain "alive" in terms of alive-count logic (not counted as dead for win conditions) — confirmed by vitals panel patches that hide them from dead lists
- Cannot talk, vote, or do tasks (buttons/chat disabled)
- Body stays at last known position (not visible to others)
- Frozen in place (non-moveable)
- Visible layer prevents rendering on other players' screens
- Cannot report bodies while devoured

> **Verified correction (read directly from `hk.cs` lines 30–133):** devour calls
> `val.Die((DeathReason)1, false)` immediately — devoured players are dead the whole time, with the
> death *hidden* (vitals patches, Ghost layer). The `b3()` murder handler (Pelican killed by someone)
> calls `val.Revive()` + teleports them to the Pelican's corpse. But `b5()`/`bc()` (other
> death/cleanup paths) just `d3.Clear()` with **no Revive**, and `a1()` (vote-out/exile path per this
> doc) re-enables movement/chat/buttons but also does **not** call `Revive()` — so devoured players
> stay dead unless the Pelican is *murdered*. Re-verify which handler is which when implementing.

**Revival on Pelican Death:**
- When Pelican dies/is voted out (called in `b3()` method when `A_0.PlayerId == ky().PlayerId`):
  - All players in `hk.d3` are revived
  - Teleported to Pelican's death position via `Coroutines.Start(val.b(ky().GetTruePosition()))`
  - Made moveable again (`val.moveable = true`)
  - Their role state is revived (`gq2.ao(A_0: false)` and `gq2.cd()`)
  - If local player: use/report buttons re-enabled, chat re-enabled
- When Pelican is voted out (called in `a1()` method):
  - All players in `hk.d3` are freed but NOT teleported
  - Made moveable, buttons/chat re-enabled
  - `hk.d3.Clear()`

**Devoured Player Teleportation:**
- **Patch:** `hw.cs` - `PelicanDevouredPlayerPatch` (HarmonyPatch on `PlayerPhysics.FixedUpdate`)
- Every frame (FixedUpdate), all devoured players in `hk.d3` are teleported to Pelican's current body position via `hk.ak(PlayerPhysics)` method
- This prevents devoured players from drifting — they follow Pelican everywhere

**Visibility Patches:**
- `VitalsPanelSetDeadPatch` (HarmonyPrefix on `VitalsPanel.SetDead`): prevents devoured players from appearing in vitals as dead (checks `dx.a(PlayerControl)` which returns `hk.d3.Contains(A_0.PlayerId)`)
- `VitalsPanelSetDisconnectedPatch` (HarmonyPrefix on `VitalsPanel.SetDisconnected`): same prevention for disconnect status

**Win Condition:**
- Neutral Killing roles typically win when all crewmates are dead and Pelican is alive (implied by role alignment, not explicitly detailed in role code — check `je.cs` or similar for full logic)
- Devoured players do NOT count as dead for crewmate-elimination calculations, so Pelican must have a way to actually eliminate them (likely when Pelican dies/leaves, or end-of-game logic)

**Edge Cases:**
- If Pelican dies while devoured players exist: all are revived at death location
- If Pelican is voted out: all devoured players are freed (non-moveable flag removed) but NOT teleported, so they stay where they were when eaten
- If Pelican dies from sabotage/etc during a devour: standard death flow applies, revived players are teleported to Pelican's death spot
- Devoured players can have other modifiers/roles applied (no exclusion logic found in modifier assignment code, though other roles like Arsonist are excluded)
- Meeting called mid-devour: devoured players remain devoured; meeting UI likely hides them anyway since they're in Ghost layer
- Devoured players cannot vent (buttons disabled)
- Cameras/admin table: devoured players invisible due to Ghost layer assignment

### Options

| Option | Default | Min | Max | Step | Notes |
|--------|---------|-----|-----|------|-------|
| Spawn Chance | 50.0 | 0.0 | 100.0 | 10.0 | Spawn probability in percent |
| Devour Cooldown | 15.0 | 10.0 | 60.0 | 2.5 | Seconds between devours |
| Can Vent | false | N/A | N/A | N/A | Boolean; false means Pelican cannot vent |

### Networking

- **RPC 89u:** `ak(PlayerControl A_0, byte A_1)` — devour action, sends target player ID
  - Payload: target player byte ID
  - Syncs to all clients
  - Updates `hk.d3` list server-side, all clients render devoured player as invisible

### Assets

- **Button Sprite:** `PelicanButton.png`
- **Death Reason Enum:** `ey.h` (custom death reason, likely displayed in end screen)
- **Layer:** "Ghost" (Unity game layer for invisible objects)
- **Color:** Hex `#6A15AB` (RGB 106, 21, 171)

---

## WITCH (Impostor Support)

**Faction:** Impostors  
**Alignment:** ImpostorSupport  
**Button Label:** `button.label.hex` (sprite: HexButton.png)  
**Color:** Impostor red (default)

### Ability Flow

**Hex Mechanic:**
- **Activation:** Cast-based ability with cast time (see "Hex Casting Duration" option). Single-target on crewmates (or anyone if "Can Hex Anyone" option enabled).
- **Effect on Hexed Player:**
  - Player added to `hk` list (List<byte> of hexed player IDs)
  - No immediate visual effect; player can continue normally during game phase
  - Hexed player sees a marker in HUD: black name color + red impostor arrow (◀) marker appended to name
  - Marker only appears if Witch is not dead/exiled

**Hexed Player Elimination:**
- **At Meeting Start:** Hexed players have a notification displayed: "Hex" with message "If you don't vote out the Witch, this crewmate will die!" (shown for each hexed player on their vote area via `bo()` method)
- **On Meeting End:** (called in `b6()` method)
  - If Witch was voted out or killed during meeting, ALL hexed players die via `ak(PlayerControl witch, PlayerControl target)` method:
    - Target calls `A_1.Exiled()` (exile animation)
    - Death logged via `e0.a(A_0, A_1, ey.f)` where `ey.f` is the hex death reason
    - Body cleanup via `j.a(A_1)` (likely removes body from tracking if needed)
    - Target's role state updated via `dx.bu(A_1)?.ao(A_0: true)` (marks as dead)
  - If Witch survives voting AND "Voting The Witch Saves All The Targets" option is ENABLED:
    - `hk.Clear()` — all hex targets are freed, no one dies
  - If "Voting The Witch Saves All The Targets" option is DISABLED:
    - All hexed players still die regardless of whether Witch survives
  - Dead/exiled hexed players are automatically removed from `hk` list before hex resolution (checks `dx.bx(val)` for exiled status)

**Hex Survival Across Meetings:**
- Hex persists across multiple meetings until:
  - Players die during meeting
  - Witch is voted out (if option enabled)
  - Witch dies for any reason (clears all hexes)

**Visibility:**
- Hexed players visible in "Are Displayed During the Game" list via `ar()` method (returns hex targets as part of role's tracked players, used for role info/end-screen tracking)

### Options

| Option | Default | Min | Max | Step | Notes |
|--------|---------|-----|-----|------|-------|
| Spawn Chance | 50.0 | 0.0 | 100.0 | 10.0 | Spawn probability in percent |
| Hex Cooldown | 30.0 | 10.0 | 120.0 | 5.0 | Seconds between hex casts |
| Additional Cooldown | 10.0 | 0.0 | 60.0 | 5.0 | Extra cooldown added to kill cooldown if "Trigger Both Cooldowns" enabled |
| Hex Casting Duration | 1.0 | 0.0 | 10.0 | 1.0 | Seconds to hold button to cast (channel time) |
| Trigger Both Cooldowns | true | N/A | N/A | N/A | If true, hexing triggers both hex AND kill cooldowns (and additional cooldown) |
| Voting The Witch Saves All The Targets | true | N/A | N/A | N/A | If true and Witch voted out, all hexed players are saved (freed without death) |
| Can Vent | true | N/A | N/A | N/A | Boolean; Witch can vent when true |

**Cooldown Interaction:** If "Trigger Both Cooldowns" is true, hexing a target triggers:
1. Hex Cooldown timer (30s default)
2. Kill Cooldown timer (Impostor's native kill cooldown)
3. Additional Cooldown added to kill cooldown

### Networking

- **RPC 92u:** `am(PlayerControl A_0, PlayerControl A_1)` — add hex target
  - Payload: target player object reference
  - Syncs hex to all clients; client adds target ID to `hk` list if not already present

- **RPC 93u:** `ak(PlayerControl A_0, PlayerControl A_1)` — execute hex death
  - Payload: victim player object reference
  - Called at meeting end if hex resolution occurs
  - Syncs death to all clients (victim is exiled, death recorded as `ey.f`)

### Edge Cases

- **Meeting called mid-cast:** Casting is interrupted by meeting call; hex effect does not apply
- **Target dies before meeting:** Dead players are auto-removed from hex list during meeting setup (checks `dx.bx(val)`)
- **Target voted out at meeting:** Removed from hex list; no death from hex
- **Witch dies mid-game:** All hexes cleared immediately (hk.Clear() in b6 if role is dead)
- **Witch dies at meeting:** Hexed players die (Witch death triggers hex resolution without "save" option check)
- **Multiple witches:** Each Witch has independent hex list; witch-specific logic (ar() method) only reports that Witch's targets
- **Comms sabotage:** No special logic; hexed players still display markers
- **Lights sabotage:** Hex markers still visible in HUD since they're part of HudManager update, not affected by light status
- **Protection (Medic, etc.):** No interaction logic found; hexes may stack with other kill mechanics depending on shield implementation

### Assets

- **Button Sprite:** `HexButton.png`
- **Hex Marker Sprite:** `HexSymbol.png` (likely displayed in HUD or over hexed player)
- **Death Reason Enum:** `ey.f` (hex death, displayed in end screen)
- **Color:** Impostor faction color (red)

---

## ASTRAL (Impostor Killing)

**Faction:** Impostors  
**Alignment:** ImpostorKilling  
**Button Label:** `button.label.astral` (sprite: AstralButton.png)  
**Color:** Impostor red (default)

### Ability Flow

**Astral Form Mechanic:**
- **Activation:** Toggleable ability. Single press activates astral form.
- **Entering Astral Form:**
  - RPC 113u calls `ak(PlayerControl A_0)` on all clients
  - Sets internal "InAstralForm" flag (`e0`) to false initially (note: flag naming suggests "exiting", but RPC handler sets this to false and starts the timer, so flag is inverted logic or starts exit routine; actual state tracked by `e2 > 0f`)
  - Sets duration timer `e2` to astral duration option value (`et`)
  - Stores current player position in `e3` (Vector2 of body position to return to)
  - Calls `ju()` to render astral state
  - State flag `e1` set to true (in astral state)

**While in Astral Form (`jx()` returns true when `e2 > 0`):**
- Duration timer `e2` decreases every frame by `Time.deltaTime`
- If Astral player dies, `e2` reset to 0 (immediately exits astral form)
- Player rendered as transparent:
  - Color set to `Color.clear` (alpha 0)
  - Exception: if local player or player is dead, alpha set to 0.1 (slightly visible to self so you can see where you are)
  - Hat and visor alpha set based on transparency level
- Player collider disabled (`ky().Collider.enabled = false`) — player can walk through walls, obstacles, other players
- Player animations frozen (IdleAnim plays continuously)
- Player invisible to other players on their screens (see Chameleon modifier logic in `kb.cs` line 104)
- Devour-check patches (`hw.cs`) skip rendering for astral players via `ba().b(RoleEnum.Astral) && ba().k()` check

**Exiting Astral Form:**
- **Automatic on Duration Expiry:** When `e2` reaches 0, `ak()` method is triggered
- **Forced Exit:** Can be forced via `ak(A_0: true)`
- **Meeting Called:** Immediately exits astral form (check in `ak()` method)
- **Exit Actions:**
  - If not meeting: player teleported back to stored position `e3` via `Coroutines.Start(ky().b(e3))`
  - Collider re-enabled
  - Color restored to opaque (alpha 1.0)
  - Hat and visor alpha set to 1.0
  - Animation reset (plays IdleAnim)
  - Player physics restored via `v.a(ky())` (resets velocity/physics state)
  - State flags reset (`e1 = false`, `e2 = 0f`)

**Killing While Astral:**
- Astral form does NOT prevent kill button usage (button initialization has `A_6: false`, suggesting kill button is NOT disabled while astral)
- However, invisibility makes it hard for other players to see the kill, so kill appears to come from nowhere
- Kill target is eliminated normally (impostor kill mechanic)
- Kill can be performed while invisible, so appears as "ghost kill" to crewmates

**Death While Astral:**
- If Astral player dies while in astral form: `e2` reset to 0 immediately (in `b3()` method), exiting form early
- Body is at last position before astral form (stored in `e3`), so death location is where they left their body
- Devoured players in astral form: if Pelican devours Astral player, they are moved to Ghost layer and become invisible (already invisible due to astral, so no visual change)

### Options

| Option | Default | Min | Max | Step | Notes |
|--------|---------|-----|-----|------|-------|
| Spawn Chance | 50.0 | 0.0 | 100.0 | 10.0 | Spawn probability in percent |
| Astral Form Cooldown | 25.0 | 10.0 | 40.0 | 2.5 | Seconds before ability can be used again after exiting form |
| Astral Form Duration | 10.0 | 5.0 | 15.0 | 1.0 | Seconds astral form lasts before auto-exit |
| Can Vent | false | N/A | N/A | N/A | Boolean; false means Astral cannot vent (unlike Witch) |

### Networking

- **RPC 113u:** `ak(PlayerControl A_0)` — activate astral form
  - Payload: player object reference (usually self)
  - Syncs astral activation to all clients
  - Updates duration timer and position on all clients

### Edge Cases

- **Meeting called during astral:** Player immediately exits form, returns to stored body position (or stays in place if meeting prevents teleportation)
- **Astral player dies:** Form exits immediately, showing body at last astral-body location (`e3`)
- **Multiple astral impostors:** Each has independent timers and body position tracking
- **Venting:** Cannot vent (option can override, but default false)
- **Comms sabotage:** No special interaction; astral form continues normally
- **Lights sabotage:** Astral player is already invisible, no additional effect
- **Protection (Medic shield):** No interaction logic found in astral code; kill shield would prevent kill as usual
- **Visibility to Admin/Cameras:** Astral player invisible due to transparency and collider disable (if using raycasting for camera visibility, disabled collider would prevent detection); however, stored position `e3` does not move, so body stays stationary where form was entered
- **Body left behind:** Yes, body position is stored at `e3` when entering astral; other players see body in last location, not moving or interacting
- **Astral inside wall:** Possible — no wall collision since collider disabled. May appear to clip through walls to other players
- **HUD Patches:** Chameleon modifier has explicit check for astral form (`kb.cs` line 104) that prevents rendering updates, keeping astral player invisible in cosmetics layer

### Assets

- **Button Sprite:** `AstralButton.png`
- **Color:** Impostor faction color (red)
- **Transparency Material:** Default (uses `Color.clear` alpha blending; may use custom shader if defined elsewhere)

---

## Mangled File Map

### Pelican (Neutral Killing)

| File | Location | Purpose |
|------|----------|---------|
| `hk.cs` | Root | Pelican role class definition, devour button setup, option creation, devour RPC handler (RPC 89u) |
| `hw.cs` | Root | Harmony patches: `PelicanDevouredPlayerPatch` (PlayerPhysics.FixedUpdate), `VitalsPanelSetDeadPatch`, `VitalsPanelSetDisconnectedPatch` — handles devoured player teleportation and vitals panel hiding |
| `gr.cs` | Root | Color definitions (`gr.Pelican`) |
| `f9.cs` | Root | Sprite assets (`n()` returns `m_c8` = PelicanButton.png) |
| `ge.cs` | Root | Role assignment logic; line 419 excludes Pelican from certain modifier target lists |
| `dx.cs` | Root | Utility method `a(PlayerControl)` checks if player is devoured via `hk.d3.Contains()` |
| `j.cs` | Root | End-screen death display (death reason ey.h shown for devoured players) |

### Witch (Impostor Support)

| File | Location | Purpose |
|------|----------|---------|
| `im.cs` | Root | Witch role class definition, hex button setup, option creation, RPC handlers (92u = add hex, 93u = execute hex death), HUD updates with hex markers, meeting phase resolution logic |
| `f9.cs` | Root | Sprite assets (`a5()` returns `m_b0` = HexButton.png; `m_bb` = HexSymbol.png) |
| `dx.cs` | Root | Utility method returns witch hex check (used in some game logic, though specific method name not isolated) |
| `ge.cs` | Root | Role assignment; Witch is ImpostorSupport, no special exclusions found |
| `gr.cs` | Root | Color definitions (uses Impostor red) |

### Astral (Impostor Killing)

| File | Location | Purpose |
|------|----------|---------|
| `hy.cs` | Root | Astral role class definition, astral form button setup, option creation, astral form toggle logic, duration/position tracking, RPC handler (113u) |
| `f9.cs` | Root | Sprite assets (`ad()` returns `m_cs` = AstralButton.png) |
| `kb.cs` | Root | Chameleon modifier rendering logic; line 104 includes check `ba().b(RoleEnum.Astral) && ba().k()` to skip rendering astral-form players (invisibility implementation) |
| `ge.cs` | Root | Role assignment; Astral is ImpostorKilling, no special exclusions found |
| `gr.cs` | Root | Color definitions (uses Impostor red) |

### Shared/Utility Files

| File | Purpose |
|------|---------|
| `AllTheRoles/Modules/Data/RoleEnum.cs` | Enum definitions: Pelican (line 73), Witch (line 39), Astral (line 41) |
| `AllTheRoles/Modules/Data/Faction.cs` | Faction enum (Neutral, Impostors) |
| `AllTheRoles/Modules/Data/RoleAlignment.cs` | Alignment enum (NeutralKilling, ImpostorSupport, ImpostorKilling) |
| `AllTheRoles.Resources.Languages.Lang.dat` | Locale strings (binary UTF-8 key-value file): option names, descriptions, button labels |

---

## Limitations & Unknowns

1. **Pelican Win Condition:** Role code does not contain explicit win logic. Likely implemented in a central game-end handler (check `je.cs` or similar). Assumed to be "eliminate all crewmates" per NeutralKilling role type.

2. **Devoured Player Death Type:** Death is recorded with custom enum `ey.h`, but exact display string (e.g., "Devoured" vs. "Eaten") depends on locale strings not fully extracted from binary file.

3. **Witch Cooldown Interaction:** "Trigger Both Cooldowns" option affects both hex cooldown and kill cooldown. The exact cooldown application order and cumulative effect on kill cooldown not fully visible in isolated code sections; trace through `a2.a()` button creation method and cooldown management to confirm.

4. **Hex Casting Duration:** Option sets a cast time (1.0s default). Exact interrupt logic (whether button release cancels, whether moving cancels, etc.) not found in `im.cs` — likely in button handler in `a2.cs` (base button logic).

5. **Astral Tether/Death:** Description says "tether to reality breaks", but mechanic is simply duration expiry + death. No special mechanic found for "broken tether" — description is flavor text only.

6. **Astral Transparency Detail:** Astral uses `Color.clear` (alpha 0) but Chameleon modifier also uses transparency. Interaction between both (if both active) not detailed — likely blends or uses alpha multiplication.

7. **Custom Death Reasons:** Enum values `ey.h` (Pelican devour) and `ey.f` (Witch hex) are not extracted. These determine how deaths are displayed on end-screen and in death-report notifications.

8. **RPC Reliability:** All RPC methods assume host authority and client sync. Edge cases (packet loss, desync, host migration) not covered.

9. **Shield/Protection Interaction:** Medic shield, Guardian Angel protection, and similar defensive mechanics may block devour/hex. Logic would be in those roles' code, not in Pelican/Witch implementation.

10. **Body Report on Devoured Players:** Devoured players do not generate reportable bodies (they're in Ghost layer and invisible). But if devoured player dies later (e.g., by actual vote at meeting), a body appears at final position. Exact behavior untested.

---

## Observations on Implementation Style

- **Obfuscation:** Code heavily obfuscated with meaningless method/class names, making tracing difficult. Single-letter or two-letter names are norm.
- **Extension Methods:** Heavy use of extension methods (`.a()`, `.b()`, etc.) for common operations (getting role, checking conditions, etc.), making code density high.
- **RPC Sync:** All role-specific actions use RPC for network sync; no client-side-only actions for core mechanics.
- **Harmony Patches:** Patches are used sparingly, mainly for hooks into base game (PlayerPhysics, VitalsPanel, etc.) rather than role-specific patching.
- **Cooldown Model:** Cooldowns abstracted through `a7` class (option system); all cooldown values are fetched from options at runtime, no hardcoding.

