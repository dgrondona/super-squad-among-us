# TOU-Mira & MiraAPI Pattern Reference for Porting Five Roles

Research findings on implementing Sniper, Ninja, Pelican, Witch, and Astral roles using TOU-Mira and MiraAPI design patterns as templates.

---

## 1. Invisibility (Ninja, Astral)

### Best Template
**Addon's `InvisibleBoyModifier` + Swooper from TOU-Mira**

#### Files
- **Addon (preferred reuse)**: `SuperSquadAmongUs/Modifiers/InvisibleBoyModifier.cs`
- **TOU-Mira reference**: `reference/TOU-Mira/TownOfUs/Modifiers/Impostor/SwoopModifier.cs`
- **Button (EffectDuration pattern)**: `reference/TOU-Mira/TownOfUs/Buttons/Impostor/SwooperSwoopButton.cs`

#### Pattern Explanation
- **Modifier base**: Extends `ConcealedModifier`, implements `IVisualAppearance` for custom appearance rendering
- **Visibility mechanics**:
  - `VisualAppearance` with cleared name, hat, skin, visor, pet, and near-transparent renderer color
  - Sets `VisibleToOthers => false` on non-owner clients
  - Uses `Player.RawSetAppearance(this)` in `OnActivate` and resets in `OnDeactivate`
  - Overrides `Player.Visible` flag on non-owner clients to hide from cameras/admin
  - Re-asserts visibility every `FixedUpdate` tick (vanilla can flip it back during vent/ladder)
- **Meeting handling**: `OnMeetingStart()` removes modifier (player is forced visible during meetings)
- **Comms sabotage**: Re-applies appearance/invisibility every tick during mushroom mixup sabotage

#### Addon's Improvements Over Swooper
- **Grace period**: 0.5-second unseen grace to prevent flicker at sight boundaries (instant reveal only)
- **Per-client visibility logic**: Computed locally by `InvisibleBoyVisibilityPatch` rather than RPC, so it works for dummy players and everyone else's Invisible Boy
- **Appearance type reuse**: Piggybacks on `TownOfUsAppearances.Swooper` to inherit comms camouflage skip

#### Gotchas
- `Player.Visible` is local-only; changing it on owner has no effect on others' rendering
- Camera/admin table honor `Player.Visible`, not sprite alpha — appearance alpha alone is insufficient
- Vanilla flips `Visible` back on during vent/ladder animations; need repeated assertions in `FixedUpdate`
- Modifier inheritance chain: `ConcealedModifier` → `BaseModifier` (not `TimedModifier`, so no auto-expiry)

---

## 2. Directional/Aimed Attack, Projectile or Hitscan (Sniper)

### Best Templates
**No exact match in TOU-Mira; hybrid approach**

#### Closest Existing Patterns
- **Kill button pattern**: `reference/TOU-Mira/TownOfUs/Buttons/Classic/Crewmate/CrewmateKilling/HunterKillButton.cs` (targeted closest player within distance)
- **Raycast utilities**: `reference/TOU-Mira/TownOfUs/Utilities/MiscUtils.cs` → `PhysicsHelpers.AnythingBetween(collider, center, position, mask, ...)`
- **Position math**: Transporter teleport uses `GetTruePosition()` and `NetTransform.SnapTo()` / `RpcSnapTo()`

#### Pattern Explanation
- **Target acquisition**: No built-in "aim in a direction" button exists; hunters/trackers use `GetClosestLivingPlayer(includeImpostors, distance, usedMinDistance, predicate)`
- **Line-of-sight check**: `PhysicsHelpers.AnythingBetween(collider, startPos, endPos, mask)` returns true if obstacle exists between two points
- **Directional firing**:
  - For snipe shots: Calculate direction from player to target via `Vector2.Normalize(targetPos - playerPos)`
  - Raycast from player position in that direction (use `Physics2D.Raycast` with `Constants.ShipOnlyMask`)
  - Kill first hit player via `RpcCustomMurder(target, MeetingCheck.OutsideMeeting)` or `RpcSpecialMurder(...)`

#### RPC Examples for Kill
- **Custom kill**: `target.RpcCustomMurder(killer, MeetingCheck.OutsideMeeting)` (in TOU-Mira's `CustomTouMurderRpcs`)
- **Special multi-kill** (e.g., blast radius): `killer.RpcSpecialMultiMurder(targetList, MeetingCheck.OutsideMeeting, true, teleportMurderer: false, ...)`

#### Gotchas
- TOU-Mira's `GetClosestLivingPlayer` can't aim in a specific direction; design likely needs custom directional button logic
- Wall penetration: Use `Constants.ShipOnlyMask` to test line-of-sight; thin walls that overlap-checks miss need `PhysicsHelpers.AnythingBetween`
- `PhysicsHelpers.AnythingBetween` + mask: Don't add extra `isTrigger`/layer filters on top of a mask; trust the mask

---

## 3. Target Marking + Tracking Arrows (Ninja Trace, Witch Hexed Markers)

### Best Templates
**Sonar `SonarArrowTargetModifier` + `ArrowTargetModifier` base class**

#### Files
- **Arrow base modifier**: `reference/TOU-Mira/TownOfUs/Modifiers/ArrowTargetModifier.cs`
- **Sonar implementation**: `reference/TOU-Mira/TownOfUs/Modifiers/Crewmate/SonarArrowTargetModifier.cs`
- **Spellslinger hex outline** (simpler alternative): `reference/TOU-Mira/TownOfUs/Modifiers/Impostor/SpellslingerHexedModifier.cs`
- **Tracker events** (optional, for kill tracking): `reference/TOU-Mira/TownOfUs/Events/Crewmate/TrackerEvents.cs`

#### Pattern Explanation - Arrows
- **ArrowTargetModifier**:
  - Extends `TimedModifier` (auto-expires after duration)
  - `OnActivate`: Calls `MiscUtils.CreateArrow(ownerTransform, color)` to spawn arrow pointing from owner to target
  - `FixedUpdate`: Updates arrow target position every tick (or configurable interval)
  - `OnDeactivate`: Destroys arrow via `_arrow.gameObject.DeepDestroy()`
  - Constructor takes `PlayerControl owner, Color color, float updateInterval`
- **Sonar subclass**:
  - Adds rainbow coloring to arrow sprite
  - Passes `HunterOptions.Instance.HunterStalkDuration` as timeout
  - Constructor forward: `SonarArrowTargetModifier(owner, color, updateInterval) : base(owner, color, updateInterval)`

#### Pattern Explanation - Outline (Simpler, no arrow)
- **SpellslingerHexedModifier**:
  - Extends `BaseModifier`, not timed (manual removal)
  - `FixedUpdate`: `Player.cosmetics.SetOutline(true, new Il2CppSystem.Nullable<Color>(hexColor))`
  - `OnDeactivate`: `Player.cosmetics.SetOutline(false, ...)`
  - Shows colored outline around target; visible to owner only (role-specific check)

#### For Ninja Trace (Hybrid)
- **Recommendation**: Combo `ArrowTargetModifier` + configurable fade:
  - Use arrow for primary tracking
  - Optional: Fade arrow opacity over duration via color alpha lerp in `FixedUpdate`
  - Set `VisibleToOthers => false` on modifier so trace only shows to Ninja

#### Gotchas
- Arrow is created in world space; destroys cleanly with `gameObject.DeepDestroy()` — important to call on deactivation
- Color must be passed as `Il2CppSystem.Nullable<Color>` when calling `SetOutline` due to IL2CPP boxing rules
- `TimedModifier` auto-expires; if you need manual control, extend `BaseModifier` instead

---

## 4. Delayed Death Resolved at Meetings (Witch Hex, Pelican Devour Fate)

### Best Templates
**MiraAPI `VotingCompleteEvent` + modifier with queued death state**

#### Files
- **MiraAPI voting events**: `reference/MiraAPI/MiraAPI/Events/Vanilla/Meeting/Voting/VotingCompleteEvent.cs`
- **MiraAPI end meeting event**: `reference/MiraAPI/MiraAPI/Events/Vanilla/Meeting/EndMeetingEvent.cs`
- **Plaguebearer infection (state-based death)**: `reference/TOU-Mira/TownOfUs/Modifiers/Neutral/PlaguebearerInfectedModifier.cs`
- **Hypnotist voting interaction** (alternative pattern): `reference/TOU-Mira/TownOfUs/Roles/Classic/Impostor/ImpostorSupport/HypnotistRole.cs` → `OnVotingComplete` hook

#### Pattern Explanation
- **Event-driven approach**:
  - Register a static event handler with `[RegisterEvent]` attribute
  - Listen to `VotingCompleteEvent` or `EndMeetingEvent` from MiraAPI
  - In handler, check if conditions met (e.g., witch still alive → kill hex targets; pelican still alive → resolve devoured fates)
- **Modifier state**:
  - Mark target with a modifier (e.g., `HexedModifier`, `DevourModifier`)
  - Modifier stores state like "queued for death" — doesn't auto-kill
  - On voting complete, iterate marked players and call `RpcCustomMurder` or equivalent
- **Voting the witch/pelican saves targets**:
  - In `OnVotingComplete` check: If killer role is exiled (voted out), don't apply deaths, remove modifiers
  - If killer role survives voting, apply deaths

#### Example Pseudocode
```csharp
[RegisterEvent]
public static void OnVotingComplete(VotingCompleteEvent @event)
{
    var witches = CustomRoleUtils.GetActiveRolesOfType<WitchRole>();
    foreach (var witch in witches)
    {
        if (witch.Player.HasDied()) continue;
        
        // Witch survived voting → kill all hex targets
        var hexedPlayers = ModifierUtils.GetActiveModifiers<HexedModifier>()
            .Where(m => m.Witch == witch.Player);
        foreach (var hex in hexedPlayers)
        {
            hex.Player.RpcCustomMurder(witch.Player, MeetingCheck.OutsideMeeting);
            hex.Player.RemoveModifier(hex);
        }
    }
}
```

#### Gotchas
- `VotingCompleteEvent` fires before exile animation; to check if someone was voted out, compare alive counts before/after
- `MeetingCheck.OutsideMeeting` is the correct enum value for death triggers during/after voting
- Modifier's `OnDeactivate` must clear visual state (e.g., outline, marker) — it's called when voting removes effects
- Host-only: Most RPC calls need `if (PlayerControl.LocalPlayer.AmOwner)` check or they run on all clients

---

## 5. Removing a Player from the Map While Keeping Them 'Alive' (Pelican Devour, Astral Body)

### Best Templates
**Transporter teleport + custom modifier to track "hidden" state**

#### Files
- **Transporter teleport RPC**: `reference/TOU-Mira/TownOfUs/Roles/Classic/Crewmate/CrewmateSupport/TransporterRole.cs` → `RpcTransport(transporter, player1, player2)` and `Transport(mono, position)` static method
- **Addon's Apparater** (simpler example): `SuperSquadAmongUs/Buttons/Crewmate/ApparaterMapButton.cs` → uses `playerControl.NetTransform.RpcSnapTo(target)`
- **Jailor jailing** (removes access): `reference/TOU-Mira/TownOfUs/Roles/Classic/Crewmate/CrewmatePower/JailorRole.cs` → adds `JailedModifier` to player

#### Pattern Explanation
- **Position removal**:
  - Teleport off-map: Use `NetTransform.SnapTo(offMapPosition)` or `RpcSnapTo(offMapPosition)` to move player far away
  - Position like `new Vector3(999f, 999f, 0f)` (far outside ship bounds) works well
  - Or use vents: `player.MyPhysics.ExitAllVents()` followed by SnapTo
- **Alive but invisible/unreachable**:
  - Add a modifier like `DevourModifier` to track state (is this player devoured?)
  - Mark as "not moveable" via `DisabledModifier` or similar to prevent walking
  - In camouflage/visibility patches, check for devouring state and hide from admin/cams
- **Admin table/camera hiding**:
  - Check `player.HasModifier<DevourModifier>()` in visibility-related patches
  - Addon's `InvisibleBoyRole` shows approach: Check modifier status in `Patches.InvisibleBoyVisibilityPatch` and skip rendering
  - Set `Player.Visible = false` for non-owners if modifier active
- **Restoring on death**:
  - `OnDeath` hook in devour modifier: Teleport back to original position or death location, remove modifier

#### RPC Pattern for Teleport
```csharp
// Send to all clients
playerControl.NetTransform.RpcSnapTo(offMapPosition);

// Or use custom RPC (if implementing Pelican-specific data)
[MethodRpc((uint)CustomRpc.PelicanDevour)]
public static void RpcDevourPlayer(PlayerControl pelican, byte targetId)
{
    var target = MiscUtils.PlayerById(targetId);
    if (target == null) return;
    
    target.NetTransform.SnapTo(offMapPosition);
    target.AddModifier<DevourModifier>(pelican);
}
```

#### Gotchas
- Off-map positions must be outside collision bounds; test in-game to verify no clipping
- `NetTransform.SnapTo()` is called locally; use RPC wrapper to sync across clients
- Devoured players should still exist in game data (not kick/remove from player list) — just unreachable
- If using modifiers to hide from admin/cams, ensure `FixedUpdate` re-asserts state every tick (as InvisibleBoy does)

---

## 6. Channel/Cast Time on an Ability (Witch Cast Duration)

### Best Template
**TownOfUs `TownOfUsRoleButton<TRole>` with `EffectDuration` and `OnEffectEnd` override**

#### Files
- **Pattern example**: `reference/TOU-Mira/TownOfUs/Buttons/Impostor/SwooperSwoopButton.cs` (toggle with effect)
- **MiraAPI base**: `reference/MiraAPI/MiraAPI/Hud/CustomActionButton.cs` (properties: `HasEffect`, `EffectDuration`, `EffectActive`)
- **Addon example**: `SuperSquadAmongUs/Buttons/Crewmate/ApparaterMapButton.cs` (map-based effect with timer rearm)

#### Pattern Explanation
- **EffectDuration property**:
  - Define as abstract property returning cast time in seconds: `public override float EffectDuration => OptionGroupSingleton<WitchOptions>.Instance.CastDuration;`
  - If `EffectDuration > 0`, `HasEffect` automatically returns `true`
- **Click flow**:
  - `OnClick()` called when player presses button
  - If `!EffectActive`, start effect: set `EffectActive = true` and `Timer = EffectDuration`
  - Button UI shows timer counting down for `EffectDuration` seconds
  - During effect, player cannot move/act normally (implementation detail)
- **On effect end**:
  - `FixedUpdate` decrements `Timer` each frame
  - When `Timer <= 0` and `EffectActive`, framework calls `OnEffectEnd()`
  - Override to apply actual ability (e.g., cast hex on targeted player)
  - Reset `EffectActive = false`

#### Example for Witch Hex Cast
```csharp
public sealed class WitchHexCastButton : TownOfUsRoleButton<WitchRole, PlayerControl>
{
    public override float EffectDuration => 
        OptionGroupSingleton<WitchOptions>.Instance.HexCastDuration;
    
    public override bool CanUse()
    {
        if (!base.CanUse()) return false;
        // Can't cast while already casting
        if (EffectActive) return false;
        return Timer <= 0 && !LimitedUses || UsesLeft > 0;
    }
    
    protected override void OnClick()
    {
        if (Target == null) return;
        // Start cast timer
        EffectActive = true;
        Timer = EffectDuration;
        // Visual: show casting bar or freeze player
    }
    
    public override void OnEffectEnd()
    {
        if (Target != null)
        {
            // Apply hex marker to target
            Target.AddModifier<HexedModifier>(PlayerControl.LocalPlayer);
            Timer = Cooldown; // Start cooldown after cast completes
        }
        EffectActive = false;
    }
}
```

#### Gotchas
- `EffectDuration > 0` is required for effect to auto-manage via timer
- If `EffectDuration == 0`, effect never auto-ends; must override `OnEffectEnd` and call manually
- `CanUse()` during effect (`EffectActive == true`) determines if button is disabled while casting
- TownOfUsRoleButton handles cooldown separately from effect timer; design both durations

---

## 7. Neutral Win Conditions (Pelican)

### Best Templates
**Glitch/Pestilence pattern with `WinConditionMet()` method**

#### Files
- **Glitch role** (pattern): `reference/TOU-Mira/TownOfUs/Roles/Classic/Neutral/NeutralKilling/GlitchRole.cs`
- **Addon's Sentinel** (worked example): `SuperSquadAmongUs/Roles/Neutral/SentinelRole.cs`
- **Win condition interface**: TOU-Mira's `ITownOfUsRole` includes `bool DidWin(GameOverReason gameOverReason)` and `bool WinConditionMet()`

#### Pattern Explanation
- **Implement `ITownOfUsRole`** on role class (already required for custom roles)
- **Override `WinConditionMet()` method**:
  ```csharp
  public bool WinConditionMet()
  {
      var pelicanCount = CustomRoleUtils.GetActiveRolesOfType<PelicanRole>()
          .Count(x => !x.Player.HasDied());
      
      if (MiscUtils.KillersAliveCount > pelicanCount)
          return false; // Too many other killing roles alive
      
      // Pelican wins if they outnumber non-Pelicans or all devoured players resolved
      return pelicanCount >= Helpers.GetAlivePlayers().Count - pelicanCount;
  }
  ```
- **Configuration**: Set `Team => ModdedRoleTeams.Custom` and `RoleAlignment => RoleAlignment.NeutralKilling` (or equivalent)
- **Entry point**: `LogicGameFlowPatches.CheckEndGame...()` in TOU-Mira iterates roles and calls `WinConditionMet()` to trigger win

#### Utilities
- `CustomRoleUtils.GetActiveRolesOfType<T>()`: Get all living instances of a role type
- `MiscUtils.KillersAliveCount`: Count all impostor-aligned players
- `Helpers.GetAlivePlayers()`: Get all non-dead players
- `player.HasDied()`: Check if player is dead

#### Gotchas
- Called every frame during gameplay; keep logic O(n) (iterate roles, not all players)
- `WinConditionMet()` only triggered if role is actually alive (in active role list)
- Return `false` if still in progress; return `true` only when win is certain
- Addon's SentinelRole uses same pattern; replicate for Pelican

---

## 8. Standard Impostor Role Template (Sniper, Ninja, Witch, Astral)

### Best Template
**Swooper role + button hierarchy**

#### Files
- **Role**: `reference/TOU-Mira/TownOfUs/Roles/Classic/Impostor/ImpostorConcealing/SwooperRole.cs`
- **Button**: `reference/TOU-Mira/TownOfUs/Buttons/Impostor/SwooperSwoopButton.cs` (extends `TownOfUsRoleButton<SwooperRole>`)
- **Modifier**: `reference/TOU-Mira/TownOfUs/Modifiers/Impostor/SwoopModifier.cs` (for ability state)
- **Options**: `reference/TOU-Mira/TownOfUs/Options/Roles/Impostor/SwooperOptions.cs`

#### Role File Structure
```csharp
public sealed class SnooperRole(IntPtr cppPtr) : ImpostorRole(cppPtr), 
    ITownOfUsRole, IWikiDiscoverable, IDoomable
{
    // Implement ITownOfUsRole properties:
    public DoomableType DoomHintType => DoomableType.SomeType;
    public string LocaleKey => "Sniper";
    public string RoleName => TouLocale.Get($"TouRole{LocaleKey}");
    public string RoleDescription => TouLocale.GetParsed($"TouRole{LocaleKey}IntroBlurb");
    public string RoleLongDescription => TouLocale.GetParsed($"TouRole{LocaleKey}TabDescription");
    
    public string GetAdvancedDescription() 
    {
        return TouLocale.GetParsed($"TouRole{LocaleKey}WikiDescription") +
               MiscUtils.AppendOptionsText(GetType());
    }
    
    // Button registry (auto-scanned):
    [HideFromIl2Cpp]
    public List<CustomButtonWikiDescription> Abilities { get; }
    
    public Color RoleColor => TownOfUsColors.Impostor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Impostor;
    public RoleAlignment RoleAlignment => RoleAlignment.ImpostorKilling; // or other
    
    public CustomRoleConfiguration Configuration => new(this)
    {
        UseVanillaKillButton = false, // Disable vanilla kill if custom button replaces it
        Icon = TouRoleIcons.Sniper,
        OptionsScreenshot = TouBanners.ImpostorRoleBanner,
        IntroSound = TouAudio.ImpostorIntroSound
    };
}
```

#### Button File Structure
```csharp
public sealed class SniperSnipeButton : TownOfUsRoleButton<SniperRole>
{
    public override string Name => TouLocale.GetParsed("TouRoleSniperSnipe", "Snipe");
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;
    public override Color TextOutlineColor => TownOfUsColors.Impostor;
    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<SniperOptions>.Instance.SnipeCooldown + MapCooldown, 5f, 120f);
    public override LoadableAsset<Sprite> Sprite => TouImpAssets.SniperSnipeSprite; // TODO: add custom sprite
    
    protected override void OnClick()
    {
        // Send RPC or modifier to snipe
        PlayerControl.LocalPlayer.RpcAddModifier<SniperSnipeModifier>();
    }
}
```

#### Options File Structure
```csharp
public sealed class SniperOptions(IGameOptions gameOptions) : AbstractOptionGroup<SniperRole>(gameOptions)
{
    [ModdedNumberOption]
    public NumberOption SnipeCooldown { get; } = new(this, "Snipe Cooldown", 30f, 5f, 120f, 5f);
    
    [ModdedNumberOption]
    public NumberOption SnipeRange { get; } = new(this, "Snipe Range", 15f, 5f, 30f, 2.5f);
    
    [ModdedToggleOption]
    public ToggleOption CanKillImpostors { get; } = new(this, "Can Kill Impostors", false);
}
```

#### Auto-Registration
- No manual registration needed; MiraAPI reflects at load time
- Roles must implement `ICustomRole` + `RoleBehaviour` (ImpostorRole already does)
- Buttons must extend `CustomActionButton` or TOU-Mira's `TownOfUsRoleButton<T>`
- Options must extend `AbstractOptionGroup<T>`
- All must be in SuperSquadAmongUs assembly

#### Addon Equivalents
Refer to `SuperSquadAmongUs/Roles/Crewmate/ApparaterRole.cs` and `SuperSquadAmongUs/Buttons/Crewmate/ApparaterMapButton.cs` for the addon's exact structure and IL2CPP quirks.

#### Gotchas
- `IntPtr cppPtr` constructor is required for IL2CPP roles; forward to base
- Don't override `Initialize`/`Deinitialize` unless calling `RoleBehaviourStubs` trampolines
- Locale keys must match XML entries in `Resources/Locale/en_US.xml`
- Sprites must be embedded resources or loaded via `LoadableResourceAsset`/`LoadableBundleAsset`

---

## 9. Meeting Announcements / Chat Messages (Sniper "Shots Fired!")

### Best Template
**`Helpers.CreateAndShowNotification()` for in-game notifications**

#### Files
- **Utility**: Used throughout TOU-Mira, e.g., `reference/TOU-Mira/TownOfUs/Buttons/Impostor/SwooperSwoopButton.cs` (line 32-35)
- **Addon example**: `SuperSquadAmongUs/Buttons/Crewmate/ApparaterMapButton.cs` uses similar pattern

#### Pattern Explanation
```csharp
var notif = Helpers.CreateAndShowNotification(
    $"<b>{text}</b>",  // Message (supports color/text markup)
    Color.white,        // Background color
    new Vector3(0f, 1f, -20f),  // Position (0,1,-20 is standard top-center)
    spr: TouRoleIcons.Sniper.LoadAsset()  // Optional icon sprite
);
notif.AdjustNotification();  // Auto-position
```

#### Variants
- **Simple text**: Just `"<b>Shots Fired!</b>"`
- **With player name**: `$"<b>{sniper.Data.PlayerName} has sniped {target.Data.PlayerName}!</b>"`
- **With color**: Use `TownOfUsColors.Impostor.ToTextColor()` to wrap text
- **Position options**: 
  - `(0, 1, -20)` top center (most common)
  - `(-2, 2, -20)` top left
  - `(2, 2, -20)` top right

#### For Announcement to All Players
- Call on the owner's client (check `PlayerControl.LocalPlayer.AmOwner`)
- Wrap in RPC if announcement needs to show on all clients:
  ```csharp
  [MethodRpc((uint)CustomRpc.SniperAnnounce)]
  public static void RpcAnnounceShot(byte sniperId, byte targetId)
  {
      var sniper = MiscUtils.PlayerById(sniperId);
      var target = MiscUtils.PlayerById(targetId);
      if (sniper == null || target == null) return;
      
      var notif = Helpers.CreateAndShowNotification(
          $"<b>{sniper.Data.PlayerName} has sniped {target.Data.PlayerName}!</b>",
          Color.yellow
      );
      notif.AdjustNotification();
  }
  ```

#### Gotchas
- Text markup supports `<color=#RRGGBBAA>...</color>` and `<b>...</b>`
- Icon sprite must be loaded; use `TouRoleIcons.Sniper.LoadAsset()` (returns `Sprite`)
- Position z-order: -20 puts it behind most UI; adjust if clipping occurs
- Notification appears client-side; for sync, wrap in RPC

---

## 10. MiraAPI RPC Pattern

### Best Template
**TOU-Mira's `[MethodRpc]` attribute with enum**

#### Files
- **Example (Transporter)**: `reference/TOU-Mira/TownOfUs/Roles/Classic/Crewmate/CrewmateSupport/TransporterRole.cs` (line 69-82)
- **Example (Hypnotist)**: `reference/TOU-Mira/TownOfUs/Roles/Classic/Impostor/ImpostorSupport/HypnotistRole.cs` (line 154-170)
- **Addon example**: None currently; addon uses vanilla `RpcSnapTo` for Apparater

#### Pattern Explanation
```csharp
// 1. Declare an enum for custom RPC IDs (if not already in TownOfUsRpc)
public enum CustomRpc : uint
{
    SniperShot = 100,
    NinjaTrace = 101,
    // ... etc
}

// 2. Define RPC method with [MethodRpc] attribute
[MethodRpc((uint)CustomRpc.SniperShot)]
public static void RpcSniperShot(PlayerControl sniper, byte targetId)
{
    // Anticheat check
    if (LobbyBehaviour.Instance)
    {
        MiscUtils.RunAnticheatWarning(sniper);
        return;
    }
    
    // Validate sender role
    if (sniper.Data.Role is not SniperRole)
    {
        Error("RpcSniperShot - Invalid sniper");
        return;
    }
    
    // Execute logic on all clients
    var target = MiscUtils.PlayerById(targetId);
    if (target != null)
    {
        target.RpcCustomMurder(sniper, MeetingCheck.OutsideMeeting);
    }
}

// 3. Call from button/ability via RPC
protected override void OnClick()
{
    if (Target == null) return;
    PlayerControl.LocalPlayer.RpcSnipe(Target.PlayerId);
    // Or shorter: call static method directly
    // RpcSniperShot(PlayerControl.LocalPlayer, Target.PlayerId);
}
```

#### RPC Calling Patterns
- **Via object extension** (if defined): `player.RpcMethodName(args)` (Reactor auto-generates)
- **Direct static call**: `ClassName.RpcMethodName(player, args)`
- **Both ways execute on all clients**, not just sender

#### MiraAPI Events as Alternative
For simpler one-time broadcasts (not requiring role-specific validation):
```csharp
[RegisterEvent]
public static void OnVotingComplete(VotingCompleteEvent @event)
{
    // Code runs on all clients automatically
}
```

#### Addon Integration
- Addon can define its own custom RPC enum (e.g., `SuperSquadRpc`)
- Use same `[MethodRpc((uint)SuperSquadRpc.PelicanDevour)]` pattern
- Anticheat: Always validate `if (LobbyBehaviour.Instance)` and role type

#### Gotchas
- RPC enum values must not collide with TOU-Mira's or other mods'
- Validate role type in RPC; malformed calls can crash
- Anticheat: Check `LobbyBehaviour.Instance` first (means not in-game)
- Host-only check: RPC runs everywhere, but some logic may need `PlayerControl.LocalPlayer.AmOwner`

---

## Recommended Template Per Ported Role

| Role | Best Pattern Match | Key Files / Notes |
|------|-------------------|-------------------|
| **Sniper (Impostor)** | Hunter kill + directional raycast | Combine HunterKillButton cooldown structure with custom `Physics2D.Raycast` for direction; use `RpcCustomMurder` for kill RPC |
| **Ninja (Impostor)** | Swooper button + SonarArrowTargetModifier | Extend `TownOfUsRoleButton<NinjaRole>` with trace arrow; use `ArrowTargetModifier` with fade option; implement trace cooldown + kill cooldown separately |
| **Pelican (Neutral)** | GlitchRole template + Transporter teleport + PlaguebearerInfectedModifier state | Neutral killing role with `WinConditionMet()`; devour via teleport off-map + modifier; resolve devoured fates at `VotingCompleteEvent` |
| **Witch (Impostor)** | SwooperSwoopButton (effect) + HexedModifier + Spellslinger hex | Use `EffectDuration` for cast timer; `HexedModifier` marks targets; kill at `VotingCompleteEvent` if witch survives vote |
| **Astral (Impostor)** | InvisibleBoyModifier + Transporter/Jailor position hide | Toggle invisibility with `ConcealedModifier` + `IVisualAppearance`; teleport body off-map during spirit form; restore on spirit end or death |

---

## Mechanics With No Good Existing Template

**None.** All 10 mechanics have workable patterns in TOU-Mira or the addon:

1. ✓ Invisibility: Swooper + InvisibleBoyModifier (excellent)
2. ✓ Directional attack: Raycast utils exist; direction calculation is vanilla math
3. ✓ Tracking arrows: ArrowTargetModifier is generic and reusable
4. ✓ Delayed death at voting: VotingCompleteEvent + modifier state is standard
5. ✓ Off-map removal: Transporter/teleport + modifier hiding pattern
6. ✓ Channel time: EffectDuration is built into CustomActionButton
7. ✓ Neutral win: WinConditionMet() interface is standard ITownOfUsRole
8. ✓ Impostor template: Swooper or addon's own roles are perfect examples
9. ✓ Announcements: `Helpers.CreateAndShowNotification` is widespread
10. ✓ RPC pattern: `[MethodRpc]` attribute is standard TOU-Mira convention

**Custom work needed only for:**
- Specific ability logic (snipe direction calculation, trace fade math)
- Role-specific options and balance tuning
- Sprite/audio assets (use addon's existing color/sound patterns)

---

## Key IL2CPP & Namespace Gotchas (From `docs/il2cpp-gotchas.md`)

1. **Constructor requirement**: All role classes need `RoleName(IntPtr cppPtr) : BaseRole(cppPtr)` for IL2CPP injection
2. **No `base.Method()` calls**: Use `RoleBehaviourStubs.Method(this, ...)` trampolines instead for overridden vanilla methods
3. **Global usings don't transfer**: Explicitly `using` namespaces; TOU-Mira's `global using`s are scoped to TOU-Mira only
4. **Modifier patterns**: `ConcealedModifier` for invisibility-like effects; `BaseModifier` for simpler state; `TimedModifier` for auto-expiring effects

---

## Summary: Strongest Template Matches

**By confidence level:**

| Confidence | Mechanic | Template | Reuse Path |
|---|---|---|---|
| Very High | Invisibility | InvisibleBoyModifier (addon) | Copy/adapt for each role |
| Very High | Neutral win condition | GlitchRole / SentinelRole | Pattern-match `WinConditionMet()` |
| Very High | Channel/cast time | Swooper button | Subclass `TownOfUsRoleButton<T>`, set `EffectDuration` |
| High | Target marking | ArrowTargetModifier | Subclass `ArrowTargetModifier`, customize color/fade |
| High | Delayed death at voting | PlaguebearerEvents + VotingCompleteEvent | Listen to event, query modifiers, RPC kills |
| High | Meeting announcements | Helpers.CreateAndShowNotification | Copy pattern, vary text/color |
| High | Off-map hiding | Transporter + modifier | Teleport off-map + add tracking modifier |
| Medium | Directional attack | Raycast + Hunter button | Combine custom raycast with TownOfUsRoleButton |
| Medium | RPC pattern | MethodRpc attribute | Copy TOU-Mira enum pattern, assign unique ID range |

**Least template support**: Directional snipe (requires custom raycast logic not present in TOU-Mira's simple kill buttons), but raycast utilities exist in MiscUtils.

---

## Implementation Checklist

- [ ] Copy template role/button/option files (e.g., Swooper → Sniper)
- [ ] Update ITownOfUsRole properties (LocaleKey, RoleName, RoleAlignment, Team)
- [ ] Implement role-specific buttons (extends TownOfUsRoleButton<T>)
- [ ] Create modifiers for state tracking (extends BaseModifier or ConcealedModifier)
- [ ] Add option group with [ModdedNumberOption] / [ModdedToggleOption]
- [ ] Define custom RPC enum and methods (if needed)
- [ ] Add event handlers ([RegisterEvent]) for voting/meeting events
- [ ] Add sprites to Resources/RoleIcons/ and Resources/[Team]Buttons/
- [ ] Add locale strings to Resources/Locale/en_US.xml with prefix `SuperSquadRole...`
- [ ] Test auto-registration: build and check roles show in-game (don't appear = missing interface)

