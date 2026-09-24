# Super Squad Among Us — Independent Review (Phase 1)

**Reviewed:** working tree on branch `dev/more-sidemen-among-us-roles`, 2026-09-24.
**Scope:** all 140 first-party source files under `SuperSquadAmongUs/` (~10,500 LOC), plus build
configuration. `reference/` was used only as evidence for upstream contracts, never reviewed as our code.

> **Note on tree state.** The working tree contains a large uncommitted change set (the TOU-Mira
> 1.7.1→1.7.3 / MiraAPI 0.4.3→0.5.0 upgrade). This review covers the code **as it currently stands**,
> including those changes. Several findings below (SSA-003, SSA-004) concern code that upgrade touched.

## Overall health

Good. This is a carefully built codebase — unusually so for a game mod. The ability-grant architecture,
the `WalkableRegionSolver` reachability search, and the RPC-validation convention are all well designed
and well documented, and the comments explain *why* rather than restating the code. It compiles clean
(0 errors; 735 warnings, of which 1,448 lines are the pre-existing, expected CS1591 doc-comment noise
called out in `CLAUDE.md`).

The defects that exist are concentrated in three places: **win conditions that silently diverge from the
upstream roles they were ported from**, **finished assets that were never wired up**, and **an
inconsistent localization story**. None of these crash; all of them change what a player sees or can do.

### Top 5 issues

| # | ID | Issue | Severity |
|---|----|-------|----------|
| 1 | SSA-001 | All four "last one standing" neutral win conditions count Neutral Benign players as opposition, unlike the upstream roles they were ported from — the win can fail to trigger | High |
| 2 | SSA-002 | Six finished role icons are embedded in the DLL but never wired to `Configuration.Icon`; those roles show MiraAPI's default icon | High |
| 3 | SSA-003 | `PelicanRole.WinConditionMet()` mutates game state, and the only caller is host-gated — the fix it implements never runs on non-host clients | Medium |
| 4 | SSA-006 | `WalkableRegionSolver` runs a worst-case ~190k-query synchronous search on the main thread; Elusive triggers up to 3 of them on a single kill attempt | Medium |
| 5 | SSA-007 | 19 of 21 modifiers hardcode English display strings despite the addon shipping 20 locale files | Medium |

### What I could not review thoroughly

- **No runtime verification.** This is a client-side IL2CPP Unity mod with no test suite; it cannot be
  executed here. Every finding below is derived from reading the implementation and from upstream source
  at the exact pinned tags. Findings marked **unverified** need a running Among Us + BepInEx instance.
- **Multiplayer/desync behaviour** is reasoned about from host-gating and RPC locality, not observed.
  SSA-003 in particular deserves a two-client playtest.
- **Coverage depth is uneven by design.** I read ~50 files line-by-line (all of `Modules/`, all
  `Roles/Neutral/`, the button/modifier base classes, all `Patches/`, all `Events/`, `Assets/`). The
  remaining ~90 files — mostly the highly formulaic per-role `Options/` groups and the smaller
  per-role buttons and modifiers — were examined by structured scans targeting the specific defect
  classes found elsewhere (validation coverage, hardcoded strings, icon wiring, allocation in
  `FixedUpdate`, duplicated blocks) rather than read end to end. A defect in one of those files that
  falls outside those classes could have been missed.
- **Art/asset quality** (sizing, PPU correctness) was not assessed beyond checking that referenced files
  exist and that declared assets are actually referenced.

---

## High

### SSA-001 — Neutral win conditions count Neutral Benign players, diverging from the upstream roles they port

**Severity:** High
**Location:** [`Roles/Neutral/SentinelRole.cs:80`](../SuperSquadAmongUs/Roles/Neutral/SentinelRole.cs#L80),
[`Roles/Neutral/GooperRole.cs:92`](../SuperSquadAmongUs/Roles/Neutral/GooperRole.cs#L92),
[`Roles/Neutral/KirbyRole.cs:106`](../SuperSquadAmongUs/Roles/Neutral/KirbyRole.cs#L106),
[`Roles/Neutral/PelicanRole.cs:79`](../SuperSquadAmongUs/Roles/Neutral/PelicanRole.cs#L79)

**Evidence.** All four use `Helpers.GetAlivePlayers()`. The upstream roles they are explicitly ported
from use `MiscUtils.GetImpactfulLivingPlayers()` instead:

```csharp
// reference/TOU-Mira/.../NeutralKilling/GlitchRole.cs:86-93   (Sentinel is a port of this)
var glitchCount = CustomRoleUtils.GetActiveRolesOfType<GlitchRole>().Count(x => !x.Player.HasDied());
if (MiscUtils.KillersAliveCount > glitchCount || MiscUtils.KillersAliveCount == 0) return false;
return glitchCount >= MiscUtils.GetImpactfulLivingPlayers().Count - glitchCount;

// reference/TOU-Mira/.../NeutralKilling/ArsonistRole.cs:117-127  (Pelican cites "Arsonist-format")
return MiscUtils.GetImpactfulLivingPlayers().Count <= 2 && MiscUtils.KillersAliveCount == 1;
```

The two helpers differ by exactly one clause:

```csharp
// reference/TOU-Mira/TownOfUs/Utilities/MiscUtils.cs:52-60
public static List<PlayerControl> GetImpactfulLivingPlayers() =>
    [.. GameData.Instance.AllPlayers.ToArray()
        .Where(x => !x.IsDead && !x.Disconnected && x.Object && !x.Object.Is(RoleAlignment.NeutralBenign))
        .Select(x => x.Object)];

// reference/MiraAPI/MiraAPI/Utilities/Helpers.cs:27-30
public static List<PlayerControl> GetAlivePlayers() =>
    [.. GameData.Instance.AllPlayers.ToArray().Where(x => !x.IsDead && !x.Disconnected && x.Object).Select(x => x.Object)];
```

**Failure scenario.** One Sentinel plus two Survivors alive, no other killers.
*Upstream Glitch:* `GetImpactfulLivingPlayers()` = 1 (Survivors excluded) → `1 >= 1 - 1` → **wins**.
*Our Sentinel:* `GetAlivePlayers()` = 3 → `1 >= 3 - 1` → **does not win**. The Sentinel has eliminated
everyone who can contest the game but the round continues until it hunts down players who, by upstream's
own definition, cannot affect the outcome. The same arithmetic applies to Gooper and Kirby; for Pelican
the `aliveNotDevoured <= 2` threshold is likewise inflated by any living Neutral Benign.

**Impact.** Neutral Killing wins fail to trigger in exactly the end-game states they exist for. Rounds
drag; with a Jester or Survivor alive the win may be unreachable without extra kills the design did not
intend to require. Affects 4 of the 5 neutral roles.

**Suggested fix.** Replace `Helpers.GetAlivePlayers()` with `MiscUtils.GetImpactfulLivingPlayers()` in
all four win conditions. In Pelican/Kirby, keep the existing devoured/swallowed subtraction on top.
Additionally restore the `|| MiscUtils.KillersAliveCount == 0` guard in Sentinel and Gooper (see
SSA-005). This is a deliberate-looking divergence in four places, so confirm with the designer that
"Neutral Benign don't block a neutral win" is the intended rule before changing it — but the current
state does not match any of the three upstream roles cited in the code's own comments.

---

### SSA-002 — Six finished role icons are shipped but never displayed

**Severity:** High
**Location:** [`Assets/SuperSquadRoleIcons.cs`](../SuperSquadAmongUs/Assets/SuperSquadRoleIcons.cs);
11 role files with no `Icon =` in `Configuration`.

**Evidence.** `Configuration.Icon` is set in only 10 of 21 roles:

```
Roles with NO Icon assignment: AstralRole, EraserRole, GodfatherRole, MafiaJanitorRole, MafiosoRole,
                               NinjaRole, RcXdRole, SniperRole, WitchRole, PelicanRole, VultureRole
```

Cross-referencing declared icon members against usage (`grep -rn 'SuperSquadRoleIcons\.<name>'`,
excluding the declaring file) gives **0 references** for: `Witch`, `Vulture`, `Godfather`, `Mafioso`,
`MafiaJanitor`, `Eraser`, `Astral`, `Ninja`, `Sniper`, `Pelican`.

Six of those point at real, finished art that exists on disk and is embedded by the `Resources/**/*.*`
glob in the csproj:

```
Eraser.png       7109 bytes      Vulture.png     27460 bytes
Godfather.png   15830 bytes      Witch.png       66474 bytes
MafiaJanitor.png 6809 bytes      Mafioso.png     15830 bytes
```

**Impact.** Six roles with completed artwork display MiraAPI's default/fallback icon in the role menu,
role guide and wiki, while their PNGs are still paid for in DLL size. This reads as "the art was never
made", when in fact it was made and left unplugged.

**Suggested fix.** Add `Icon = SuperSquadRoleIcons.<Name>,` to `Configuration` for `WitchRole`,
`VultureRole`, `GodfatherRole`, `MafiosoRole`, `MafiaJanitorRole`, `EraserRole`. For `AstralRole`,
`NinjaRole`, `SniperRole`, `PelicanRole`, `RcXdRole` — which have only placeholder members — either wire
the placeholder explicitly (matching `DetonatorRole`/`DumperRole`/`KirbyRole`, which do) or delete the
unused members. Verify in-game that each role's menu entry shows its own art.

---

## Medium

### SSA-003 — `PelicanRole.WinConditionMet()` mutates game state from a predicate, and its only caller is host-only

**Severity:** Medium
**Location:** [`Roles/Neutral/PelicanRole.cs:91-98`](../SuperSquadAmongUs/Roles/Neutral/PelicanRole.cs#L91-L98)

**Evidence.** The predicate kills players as a side effect:

```csharp
if (winConditionMet)
{
    // Force the meeting-digest pass now, in case the threshold was hit mid-round with no
    // meeting ever called - otherwise devoured players never get Exiled() ...
    PelicanEvents.DigestStomach();
}
```

`DigestStomach()` calls `target.Exiled()` (a client-local kill) on every devoured player. The only
caller of `WinConditionMet()` besides `DidWin` is TOU-Mira's `NeutralRoleWinCondition.IsMet`, reached
through `LogicGameFlowNormal.CheckEndCriteria` — which TOU-Mira hard-gates to the host:

```csharp
// reference/TOU-Mira/TownOfUs/Patches/LogicGameFlowPatches.cs:188-191
if (!AmongUsClient.Instance.AmHost)
{
    return false;     // skips the original, so no win condition is evaluated off-host
}
```

**Failure scenario.** A non-host Pelican hits its win threshold mid-round with no meeting having been
called. The digest runs only on the host. On every other client the devoured players are never
`Exiled()`: their local `GameHistory.PlayerStats` still records them as `Alive`, and they remain frozen
under `CarriedModifier` until the game-over screen. The end-game summary therefore disagrees between
host and clients — which is precisely the symptom the comment says this code exists to prevent.

**Impact.** Incorrect end-game summary on all non-host clients whenever a Pelican wins without a meeting.
Cosmetic rather than game-breaking, but it is a real host/client divergence, and the code reads as if it
were solved.

**Suggested fix.** Move the forced digest out of the predicate. Either (a) have the host broadcast the
digest over an RPC so every client performs it, or (b) trigger it from a game-over/`TriggerGameOver`
hook that runs everywhere. Keeping a state mutation inside a predicate that is polled every tick is
fragile regardless of the host-gating; it also means `DidWin()` can kill players during game-over
evaluation.

**Verification:** two-client lobby, Pelican devours to threshold without calling a meeting, compare the
end-game summary on host vs. client. **Unverified** as written.

---

### SSA-004 — `KirbyRole` omits the forced digest that `PelicanRole` performs

**Severity:** Medium
**Location:** [`Roles/Neutral/KirbyRole.cs:82-109`](../SuperSquadAmongUs/Roles/Neutral/KirbyRole.cs#L82-L109)

**Evidence.** Kirby's `WinConditionMet()` copies Pelican's counting logic — its own comment says so
("same reasoning as `PelicanRole.WinConditionMet`") — but does not copy the `DigestStomach()` call.
`KirbyEvents` has the equivalent digest routine, invoked only from its `StartMeetingEvent` handler.

**Failure scenario.** Kirby reaches its win threshold mid-round with no meeting called. Swallowed
players are never digested: they show as `Alive` in the end-game summary and stay frozen under
`CarriedModifier`. This is the exact bug Pelican's forced digest was added to fix.

**Impact.** Same class as SSA-003, but unmitigated rather than partially mitigated.

**Suggested fix.** Resolve together with SSA-003 — whatever mechanism replaces Pelican's in-predicate
digest should be applied to Kirby too. Do not simply copy the current Pelican approach, since it carries
the host-only defect.

---

### SSA-005 — Sentinel and Gooper drop the `KillersAliveCount == 0` guard present upstream

**Severity:** Medium
**Location:** [`Roles/Neutral/SentinelRole.cs:75-80`](../SuperSquadAmongUs/Roles/Neutral/SentinelRole.cs#L75-L80),
[`Roles/Neutral/GooperRole.cs:87-92`](../SuperSquadAmongUs/Roles/Neutral/GooperRole.cs#L87-L92)

**Evidence.** Upstream `GlitchRole.cs:88` guards `if (MiscUtils.KillersAliveCount > glitchCount || MiscUtils.KillersAliveCount == 0) return false;`.
Ours keeps only the first clause.

**Failure scenario.** With zero living killers and zero living Sentinels, `glitchCount == 0` and
`KillersAliveCount == 0`, so the first guard passes; the return becomes `0 >= aliveCount - 0`, which is
true when `aliveCount == 0`. A dead Sentinel is then reported as having met its win condition. Reaching
a state with no living players at all is rare, which is why this is Medium and not High — but the guard
exists upstream precisely to make the predicate total.

Note also that neither role checks `Player.HasDied()` first (Pelican and Kirby both do), so
`WinConditionMet()` is a global predicate that returns the same answer for a dead instance as a living
one. `DidWin()` returns it directly.

**Suggested fix.** Add the `|| MiscUtils.KillersAliveCount == 0` clause to both, and consider a leading
`if (Player.HasDied()) return false;` for consistency with the other two neutral roles — confirming
first whether a dead Sentinel *should* share in a teammate Sentinel's win.

---

### SSA-006 — Reachability search is an unbounded synchronous main-thread cost

**Severity:** Medium
**Location:** [`Modules/WalkableRegionSolver.cs:171-209`](../SuperSquadAmongUs/Modules/WalkableRegionSolver.cs#L171-L209);
callers at [`Events/ElusiveEvents.cs:81`](../SuperSquadAmongUs/Events/ElusiveEvents.cs#L81) and
[`Buttons/Crewmate/ApparaterMapButton.cs:160`](../SuperSquadAmongUs/Buttons/Crewmate/ApparaterMapButton.cs#L160)

**Evidence.** The expansion loop is bounded by `MaxExpandedCells = 8000`. Each expanded cell iterates 8
neighbours, and each neighbour performs `IsOpen` (a `Physics2D.OverlapCircle`) plus `HasClearEdge` (a
`Physics2D.Linecast`), with a second `IsOpen` for landing validation on improving cells:

```csharp
while (frontier.Count > 0 && expanded < MaxExpandedCells)   // up to 8000
    foreach (var (dx, dy) in Neighbors)                     // x8
        if (!IsOpen(...) || !HasClearEdge(...))             // >=2 physics queries each
```

Worst case ≈ 8,000 × 8 × 2–3 ≈ **130k–190k physics queries in a single frame**, with no coroutine
yielding. `TryFindRandomReachablePoint` (lines 245-268) calls the whole search up to
`RandomAttempts = 3` times, and re-runs `CollectDoorColliderIds()` — four
`GetComponentsInChildren` sweeps — per attempt.

`ElusiveEvents.CheckForElusiveShield` invokes the 3-attempt variant synchronously inside a
`BeforeMurderEvent` handler, i.e. at the instant of a kill attempt.

**Impact.** A potentially multi-hundred-millisecond frame hitch at two gameplay-critical moments: the
Apparater's teleport click, and any kill attempt on a shielded Elusive. The bound only bites when the
target is unreachable (the search then explores the whole connected region before giving up), which is
also the case where the player is most likely to retry.

**Suggested fix.** Measure first — instrument `TryFindReachablePoint` with a stopwatch and log the
elapsed time and `expanded` count on the two real call paths, on the largest map (Airship). If the
worst case is material: hoist `CollectDoorColliderIds()` out of the per-attempt loop in
`TryFindRandomReachablePoint`; lower `MaxExpandedCells`; and/or cache `IsOpen` results per cell (the
same cell centre is currently probed once as a neighbour and again as a landing candidate).

**Unverified** — this is a static worst-case bound, not a measurement. Typical-case cost may be far
lower because the early-success break at line 179 fires as soon as a cell lands within one cell of the
target.

---

### SSA-007 — Modifier display strings are hardcoded English while the addon ships 20 locale files

**Severity:** Medium
**Location:** all of [`Modifiers/`](../SuperSquadAmongUs/Modifiers/)

**Evidence.** 19 of 21 modifiers hardcode `ModifierName`; only the two universal game modifiers
(`SlideTackleModifier`, `InvisibilityCloakModifier`) use a locale key:

```
"Cloaked"  "Astral Linger"  "Carried (Incapacitated)"  "Astral Form"  "Hexed"  "Devoured"
"Ninja Invisible"  "Hidden in Cloak"  "Vested"  "Invisible"  "Swallowed"  "Shielded"
"Carrying"  "Bombed"  "Swooped"  "Erased"  "Ninja Marked"  "Tackled"  "Protected"
```

At least 8 `GetDescription()` overrides return hardcoded sentences, e.g.
`Modifiers/DevouredModifier.cs:22` `return "You have been devoured by the Pelican!";`

By contrast every role fully localizes `RoleName`/`RoleDescription`/`RoleLongDescription`, and
`Resources/Locale/` contains 20 language files. `Options/Modifiers/SuperSquadModifierOptions.cs:17`
has the same issue for `GroupName => "Super Squad Modifiers"` while its two sibling groups localize.

**Impact.** These strings are player-visible (modifier UI, modifier descriptions, the options menu).
Non-English players get a half-translated mod. It also means adding a translation cannot cover modifiers
at all without a code change.

**Suggested fix.** Move each to `MiraLocaleManager.Get("SuperSquadModifier<Name>", "<current English>")`
and add the keys to `Resources/Locale/en_US.xml`. Passing the current literal as the fallback makes the
change behaviour-preserving even before the keys are added. Note the `SuperSquad*` key prefix is what
keeps us from colliding with TOU-Mira's ~2,900 keys (a collision logs an error and silently overwrites).

---

### SSA-008 — Culture-sensitive `EndsWith` in a per-frame name rewrite can append tags repeatedly

**Severity:** Medium
**Location:** [`Patches/MafiaLabelsPatch.cs:35`](../SuperSquadAmongUs/Patches/MafiaLabelsPatch.cs#L35),
[`Patches/MafiaLabelsPatch.cs:60`](../SuperSquadAmongUs/Patches/MafiaLabelsPatch.cs#L60)

**Evidence.** Build emits CA1310 at both sites:

```
Patches/MafiaLabelsPatch.cs(35,18): warning CA1310: The behavior of 'string.EndsWith(string)' could
vary based on the current user's locale settings.
```

```csharp
if (!player.cosmetics.nameText.text.EndsWith(tag))     // CurrentCulture comparison
{
    player.cosmetics.nameText.text += $" {tag}";       // runs every HudManager.Update frame
}
```

The idempotence of this patch depends entirely on that comparison. `string.EndsWith(string)` uses
`CurrentCulture`, not the `TownOfUsPlugin.Culture` the plugin otherwise standardises on
(`SuperSquadAmongUsPlugin.cs:27`). Under a culture whose collation treats the parenthesised tag as
ignorable, the check can fail against text it should match, and the append then runs **once per frame**
— unbounded name growth at 60 fps.

**Impact.** Locale-dependent. Worst case is a runaway name string for mafia members on affected clients.

**Suggested fix.** Use `EndsWith(tag, StringComparison.Ordinal)` at both sites. This is the correct
comparison for a literal marker regardless, and silences the analyzer.

**Related, unverified:** the class comment assumes TOU-Mira *rewrites* names rather than appending to
them ("name rewrites by TOU-Mira just lose the tag for a single frame"). If any other patch appends a
suffix *after* our tag on each frame, `EndsWith` fails every frame and the same unbounded growth occurs
independent of culture. Confirming this needs a live game with a mafia member holding a role whose name
is decorated by TOU-Mira (e.g. a revealed Mayor).

---

### SSA-009 — `FindObjectsOfType<DeadBody>()` runs every physics tick in two buttons

**Severity:** Medium
**Location:** [`Buttons/Neutral/VultureEatButton.cs`](../SuperSquadAmongUs/Buttons/Neutral/VultureEatButton.cs)
(`SyncArrows`, called from `FixedUpdate`),
[`Buttons/Neutral/GooperGoopButton.cs`](../SuperSquadAmongUs/Buttons/Neutral/GooperGoopButton.cs) (same pattern)

**Evidence.** Both buttons call, from `FixedUpdate` (50 Hz):

```csharp
foreach (var body in UnityEngine.Object.FindObjectsOfType<DeadBody>())
```

`FindObjectsOfType` is a full scene-graph scan and is the standard Unity performance footgun. It also
allocates an array per call. `SuperSquadBodies.DestroyBodies` uses the same call, but only once per
ability use, which is fine.

**Impact.** 50 full scene scans per second for the duration of a Vulture's or Gooper's life, plus
per-tick array allocation feeding GC pressure in an IL2CPP runtime.

**Suggested fix.** Dead bodies change rarely. Either poll on a slower cadence (every N ticks), or track
bodies via the existing `DeadBody` lifecycle (bodies are created on murder and destroyed at meeting
start / on clean), or reuse whatever body cache TOU-Mira already maintains if one exists. Behaviour to
preserve: arrows appear for new bodies and disappear when a body is reported, cleaned or eaten.

---

## Low

### SSA-010 — Dead code: the Sniper's projectile-visual machinery

**Severity:** Low
**Location:** [`Modules/SniperShots.cs:115-157`](../SuperSquadAmongUs/Modules/SniperShots.cs#L115-L157)

**Evidence.** `RpcShowShot`, `ShowShotLocally`, `CoBulletTravel`, and the `VisualRange`/`VisualSpeed`
constants have zero callers outside the file (verified by grep across all 140 source files). The class
comment admits it: *"unused travel visual machinery … kept for a future role that wants a visible
projectile."* Two consequences follow:

- `SuperSquadRpc.ShowSniperShot` is a live, registered RPC with **no sender validation** — the only one
  of the 11 RPCs lacking it, and the only such gap not documented as deliberate (`RpcMoveCar` and
  `RpcDespawnCar` both carry explanatory comments). Any client can spawn a sprite on every other client.
- `SuperSquadImpAssets.SniperGuideSprite` and its `SniperGuide.png` are referenced only from the dead
  coroutine, so the image is embedded for nothing.

**Suggested fix.** Delete the four members, the RPC enum entry, the sprite member and the PNG. Git
history preserves it if a future role wants it back. If it is genuinely wanted soon, at minimum add the
missing `AbilityGrants.SenderIsOrHolds<SniperRole>` guard so a dead code path is not also an unvalidated
network entry point.

### SSA-011 — Duplicate role art shipped twice

**Severity:** Low
**Location:** `SuperSquadAmongUs/Resources/RoleIcons/Godfather.png`, `.../Mafioso.png`

**Evidence.** Byte-identical (`md5 8f93908bc953406577303d8fef7fcf1f`, 15,830 bytes each).

**Impact.** ~15 KB of duplicated payload, and two roles that will look identical in the menu once
SSA-002 is fixed.

**Suggested fix.** Decide whether Mafioso needs distinct art. If not, have both icon members point at
one file. If yes, this is an art task, not a code one — track it in `docs/roles/`.

### SSA-012 — Unused declarations

**Severity:** Low

| Declaration | Location | Evidence |
|---|---|---|
| `SuperSquadColors.Chameleon` | [`SuperSquadColors.cs:10`](../SuperSquadAmongUs/SuperSquadColors.cs#L10) | 0 references; no Chameleon role exists |
| `SuperSquadNeutAssets.VultureArrowSprite` | [`Assets/SuperSquadNeutAssets.cs`](../SuperSquadAmongUs/Assets/SuperSquadNeutAssets.cs) | 0 references; `VultureEatButton` uses `MiscUtils.CreateArrow(..., Color.blue)` instead |
| 9 `SuperSquadRoleIcons` members | [`Assets/SuperSquadRoleIcons.cs`](../SuperSquadAmongUs/Assets/SuperSquadRoleIcons.cs) | 0 references — see SSA-002; 6 of them should be *wired up*, not deleted |

**Suggested fix.** Delete `Chameleon` and `VultureArrowSprite` (and its PNG if unused elsewhere). Resolve
the icon members via SSA-002 first, then delete whichever remain genuinely unused.

### SSA-013 — Copy-paste identifiers left over from the ported upstream roles

**Severity:** Low
**Location:** [`Roles/Neutral/SentinelRole.cs:73`](../SuperSquadAmongUs/Roles/Neutral/SentinelRole.cs#L73),
[`Roles/Neutral/SentinelRole.cs:86-87`](../SuperSquadAmongUs/Roles/Neutral/SentinelRole.cs#L86-L87)

**Evidence.** `var glitchCount = CustomRoleUtils.GetActiveRolesOfType<SentinelRole>()...` — named after
TOU-Mira's Glitch. In `OffsetButtons`, `var douse = CustomButtonSingleton<SentinelExplodeButton>.Instance;`
and `var ignite = CustomButtonSingleton<SentinelKillButton>.Instance;` — named after the Arsonist's
douse/ignite, and additionally swapped relative to their meanings (`douse` holds the *explode* button).

**Impact.** Misleading to a reader; `douse`/`ignite` actively suggest the wrong ability.

**Suggested fix.** Rename to `sentinelCount`, `explodeButton`, `killButton`. Pure rename, no behaviour
change.

### SSA-014 — `SniperShots` class comment contradicts the implementation

**Severity:** Low
**Location:** [`Modules/SniperShots.cs:15-16`](../SuperSquadAmongUs/Modules/SniperShots.cs#L15-L16) vs
[`Modules/SniperShots.cs:72-75`](../SuperSquadAmongUs/Modules/SniperShots.cs#L72-L75)

**Evidence.** The comment describes *"an infinite line through the sniper's body and clicked point"*.
The implementation rejects anything behind the shooter:

```csharp
var along = Vector2.Dot(toPlayer, direction);
if (along < -radius) continue;
```

That is a ray, not a line: a player standing directly behind the sniper is never hit.
`SniperSnipeButton`'s own comment correctly says "from the sniper's body through the click point".

**Impact.** Documentation defect only — but it is the kind that leads a future change in the wrong
direction. Which behaviour is *intended* is a design question I could not resolve from the code.

**Suggested fix.** Correct the comment to describe a forward ray, or change the code if an infinite line
was intended. Confirm intent against `docs/roles/sniper.md` and the original TOR behaviour.

### SSA-015 — Triplicated `GetVisualAppearance()`

**Severity:** Low
**Location:** [`Modifiers/CarriedModifier.cs:52-67`](../SuperSquadAmongUs/Modifiers/CarriedModifier.cs#L52-L67),
[`Modifiers/TimedInvisibilityModifier.cs:41-56`](../SuperSquadAmongUs/Modifiers/TimedInvisibilityModifier.cs#L41-L56),
[`Modifiers/InvisibleBoyModifier.cs:43-58`](../SuperSquadAmongUs/Modifiers/InvisibleBoyModifier.cs#L43-L58)

**Evidence.** The three 16-line method bodies are **byte-identical** (same MD5,
`89e1178a068c946cc3af03999b449309`). `ApplyLocalVisibility` is likewise duplicated three times, differing
only in access modifier and comments. The only genuine variation is `LocalViewerSeesOutline()`, which is
already virtual in one of the three.

**Impact.** Three places to keep in sync; the blank-appearance recipe (hat/skin/visor/pet/name ids) is a
detail that upstream changes could invalidate in all three at once.

**Suggested fix.** Extract a shared helper — e.g. a static
`SuperSquadAppearances.BuildBlanked(BaseModifier owner, bool seesOutline)` — and have all three call it.
See CLEANUP_PLAN.md for sequencing; this must not change the appearance type
(`TownOfUsAppearances.Swooper`), which is load-bearing for comms-camouflage behaviour.

### SSA-016 — `KeybindArbiter` click boilerplate duplicated four times

**Severity:** Low
**Location:** [`Buttons/SuperSquadRoleButton.cs:40-48`](../SuperSquadAmongUs/Buttons/SuperSquadRoleButton.cs#L40-L48)
and `:74-82`; [`Buttons/GrantedAbilityButtons.cs:74-82`](../SuperSquadAmongUs/Buttons/GrantedAbilityButtons.cs#L74-L82)
and the variant at `:203-226`.

**Evidence.** The same `if (!CanClick() || !KeybindArbiter.TryClaim(Keybind)) return;` guard appears in
four `ClickHandler` overrides, three of them character-identical.

**Impact.** Low, but this guard has a documented sharp edge (`KeybindArbiter.TryClaim` mutates state and
must never be called from a predicate), so having it restated four times increases the chance a future
copy gets it subtly wrong.

**Suggested fix.** The three generic bases derive from different TOU-Mira bases, so a common base class
is not available; a small static helper (`KeybindArbiter.TryFire(button)`) that encapsulates the
`CanClick() + TryClaim` pair would collapse the duplication without changing the type hierarchy.

### SSA-017 — Per-frame allocation in the Mafioso gate

**Severity:** Low
**Location:** [`Patches/MafiosoGatePatches.cs:18-22`](../SuperSquadAmongUs/Patches/MafiosoGatePatches.cs#L18-L22)

**Evidence.** `LocalMafiosoIsGated` calls `Helpers.GetAlivePlayers()` — which materialises a new
`List<PlayerControl>` via LINQ over `GameData.Instance.AllPlayers` — and is evaluated once per
`HudManager.Update` (60 Hz) for a local Mafioso, plus on every `KillButton.DoClick`.

**Impact.** Minor GC churn, confined to one role. Correctly early-returns for non-Mafioso players, so
there is no cost for anyone else.

**Suggested fix.** Replace with a non-allocating scan over `PlayerControl.AllPlayerControls` looking for
a living `GodfatherRole`, or cache per frame.

### SSA-018 — Analyzer-flagged leftovers

**Severity:** Low

| Warning | Location | Note |
|---|---|---|
| CS8618 | [`Modules/Explode.cs:12`](../SuperSquadAmongUs/Modules/Explode.cs#L12) | `Transform` is non-nullable but never assigned in a constructor; the class is only ever built via `CreateExplode`, so make it `required`/`init` or restructure to a constructor |
| S125 | [`Buttons/Impostor/WitchHexButton.cs:111`](../SuperSquadAmongUs/Buttons/Impostor/WitchHexButton.cs#L111) | commented-out code |
| S125 | [`Events/EraserEvents.cs:78`](../SuperSquadAmongUs/Events/EraserEvents.cs#L78) | commented-out code |

These are the only non-CS1591 warnings in the build besides the two CA1310s in SSA-008 — i.e. the build's
signal-to-noise is genuinely good once doc-comment warnings are set aside.

---

## Deliberate patterns I checked and am *not* reporting as defects

Recording these so Phase 2 does not "fix" them:

- **`Configuration => new(this)` allocating on every access** (all 21 roles). All 106 TOU-Mira roles use
  the identical expression-bodied pattern; this is the upstream idiom, not our defect.
- **`SuperSquadGooper.RpcGoop` using a raw `is not GooperRole` check** instead of
  `AbilityGrants.SenderIsOrHolds`. Correct here: goop is Gooper's core identity ability and
  `ApplyPortableGrant` deliberately never transfers a grant-holder's own kit, so there can be no
  legitimate borrower — and the handler needs the concrete instance to mutate `GoopedBodyIds`.
- **`RcXdCar.RpcMoveCar` / `RpcDespawnCar` lacking sender validation.** Both carry comments explaining
  why (they legitimately fire after the owner's role has swapped to a ghost).
- **`LogoPatch` replacing `TouAssets.Banner` wholesale.** I traced the blast radius: within TOU-Mira,
  `TouAssets.Banner` is read only by its main-menu `LogoPatch` and the April Fools patch. Role option
  screenshots use `TouBanners.*`, so this does not bleed into TOU's own role menus. (Minor cosmetic note:
  `BannerDark`, used by TOU's loading screen, is not patched, so the loading screen keeps TOU's artwork.)
- **`SightChecker.CanAnyoneSee` cost.** It looked like a hot path (`PlayerControl.FixedUpdate` postfix for
  every player), but the raycast is short-circuited behind a cheap distance test, and the patch filters to
  `InvisibleBoyRole` before doing any work.
