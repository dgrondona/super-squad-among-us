# Super Squad Among Us — Independent Review (Phase 1)

**Reviewed:** commit `2623413` on branch `dev/more-sidemen-among-us-roles`, 2026-09-24.
**Scope:** all 140 first-party source files under `SuperSquadAmongUs/` (~10,500 LOC), plus build
configuration. `reference/` was used only as evidence for upstream contracts, never reviewed as our code.

> **Note on tree state.** This review covers the code as of commit `2623413` ("adjusted to update in
> line with depedancies"), which landed the TOU-Mira 1.7.1→1.7.3 / MiraAPI 0.4.3→0.5.0 upgrade. All
> line numbers are against that commit; the working tree is clean. Several findings below (SSA-003,
> SSA-004) concern code that upgrade touched.

## Second pass (2026-09-24)

After the initial sweep, every finding was re-checked against the per-role design docs in `docs/roles/`
to separate *bugs* from *recorded design decisions*. That pass changed three findings and added four:

- **SSA-010 was wrong to recommend deletion** — `sniper.md` records a dated user decision to keep the
  Sniper's projectile machinery. Corrected below.
- **SSA-006 understated what already exists** — `apparater.md` shows the search is already instrumented
  and already names the optimisations I proposed. Corrected below.
- **SSA-013 understated the residue** — `sentinel.md` lists more than I found. Expanded below.
- **New: SSA-019 – SSA-022**, including a logic bug in the reachability search and two design docs that
  assert invariants the code does not hold.

**Pass 3 (in progress)** — the targeted read of the ability implementations planned in
[PASS_3_PLAN.md](PASS_3_PLAN.md) — has so far added **SSA-023 – SSA-026** and cleared
`ElusiveShieldButton`, `AstralFormButton` and `DaddyHagridHideButton`.

### Decisions taken

Confirmed with the maintainer; Phase 2 should implement these rather than re-litigate them.

| Topic | Decision |
|---|---|
| Neutral win conditions (SSA-001, SSA-005) | **Match upstream.** Switch all four to `MiscUtils.GetImpactfulLivingPlayers()` and restore the `KillersAliveCount == 0` guard. Neutral Benign players must not block a Neutral Killing win. |
| Apparater (SSA-019, SSA-006) | **Fix the `visited` bug *and* cache the walkable region per round.** The traversal graph is door-independent, so it is invariant for a round and can be built off the critical path. |
| Vulture kit borrowing (SSA-020) | **Exclude `VultureRole` from `KitExcludedRoles`'s inverse** — i.e. add it to the exclusion list, as Godfather/Mafioso already are. |
| Modifier localization (SSA-007) | **Convert all of them**, names and descriptions, plus `SuperSquadModifierOptions.GroupName`. |

## Overall health

Good. This is a carefully built codebase — unusually so for a game mod. The ability-grant architecture,
the `WalkableRegionSolver` reachability search, and the RPC-validation convention are all well designed
and well documented, and the comments explain *why* rather than restating the code. It compiles clean:
**0 errors and 735 warnings, 724 of which are the pre-existing, expected CS1591 doc-comment noise**
called out in `CLAUDE.md`. Of the remaining 11, six are CA1707 false positives (Harmony's mandatory
`__instance` / `__result` parameter names) — leaving just five real warnings, all itemised below.

The defects that exist are concentrated in three places: **win conditions that silently diverge from the
upstream roles they were ported from**, **finished assets that were never wired up**, and **an
inconsistent localization story**. None of these crash; all of them change what a player sees or can do.

### Top 5 issues

| # | ID | Issue | Severity |
|---|----|-------|----------|
| 1 | SSA-019 | `WalkableRegionSolver` blacklists a grid cell before testing it, so a cell rejected for one bad *step* is permanently excluded even when a clear step to it exists — the likely cause of Apparater feeling unreliable | High |
| 2 | SSA-001 | All four "last one standing" neutral win conditions count Neutral Benign players as opposition, unlike the upstream roles they were ported from — the win can fail to trigger | High |
| 3 | SSA-002 | Six finished role icons are embedded in the DLL but never wired to `Configuration.Icon`; those roles show MiraAPI's default icon | High |
| 4 | SSA-003 | `PelicanRole.WinConditionMet()` mutates game state, and the only caller is host-gated — the fix it implements never runs on non-host clients, contradicting what `pelican.md` claims | Medium |
| 5 | SSA-022 | Two design docs assert invariants the code does not hold, which is *why* SSA-001 and SSA-003 went unnoticed | Medium |

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
- **Second pass closed the docs gap.** Every finding has now been cross-checked against
  `docs/roles/*.md`, `docs/modifiers/*.md` and `docs/architecture.md`, which is what caught the three
  corrections and SSA-022. The three architectural rules in `gooper.md` were re-audited against the code
  rather than taken on trust: rule 2 (RPC validators) and rule 3 (handlers key on caster, not role type
  — `WitchEvents` and `DaddyHagridEvents` both verified) hold; rule 1 (state on the button singleton)
  has one live and one latent violation (SSA-020, SSA-021).

---

## High

### SSA-019 — The reachability search blacklists a cell before testing it, permanently excluding reachable ground

**Severity:** High
**Location:** [`Modules/WalkableRegionSolver.cs:185-198`](../SuperSquadAmongUs/Modules/WalkableRegionSolver.cs#L185-L198)

**Evidence.** The expansion loop marks a neighbour visited *before* either validity check runs:

```csharp
foreach (var (dx, dy) in Neighbors)
{
    var neighborCell = (Cx: curCell.Cx + dx, Cy: curCell.Cy + dy);
    if (!visited.Add(neighborCell))      // <-- blacklisted here, unconditionally
    {
        continue;
    }

    var neighborCenter = CellCenter(neighborCell);

    if (!IsOpen(neighborCenter, traversalRadius, true) ||     // node property  (permanent)
        !HasClearEdge(curCenter, neighborCenter))             // EDGE property  (this step only)
    {
        continue;                                             // ...but the cell stays blacklisted
    }
```

The two checks have different scopes. `IsOpen` asks "is this cell clear?" — a permanent property of the
cell, so caching a failure is correct. `HasClearEdge(from, to)` asks "can I step from *this particular*
cell to that one?" — a property of the **pair**. Conflating them means a cell is permanently excluded by
whichever neighbour happened to reach it first, even when it was rejected for a reason specific to that
one step.

**Failure scenario.** Cell **X** sits just past a wall corner. Cell **A** is diagonally adjacent and
marginally closer to the click, so the distance-keyed frontier expands it first; the diagonal step A→X
clips the corner collider, so `HasClearEdge` fails — but X is already in `visited`. Cell **B**,
orthogonally adjacent to X with a completely clear step, is expanded later and is refused at line 188.
**X is never evaluated**, though it is open ground reachable in one clean step.

**Impact.** The search returns `bestCell`, the closest *accepted* cell. When the true-closest cell is
punched out this way the teleport either lands noticeably off from the click, or — because
`ApparaterMapButton` rejects any result farther than the snap cap
([`ApparaterMapButton.cs:170-176`](../SuperSquadAmongUs/Buttons/Crewmate/ApparaterMapButton.cs#L170-L176),
1.5u fallback / 6u in-room) — the click is discarded and nothing happens at all. That is the
"clicked a valid spot and got nothing" shape of unreliability. Corner and doorway geometry is exactly
where diagonals get tried first, so this biases toward failing near the tight spots players aim at.

`Modules/WalkableRegionSolver.cs` is shared, so `ElusiveEvents`' random teleport inherits the same
defect.

**Confidence.** The defect is certain from the control flow. Its *frequency* is geometry-dependent and
I could not quantify it without running the game — but the ability already logs every rejection and the
search duration, so `BepInEx/LogOutput.log` will show whether rejections dominate.

**DECIDED — fix, and pair it with the per-round region cache** (SSA-006). The fix is to split the two
concerns: keep a permanent per-cell node verdict (also removing today's duplicate probing), and treat a
failed edge as rejecting only that step, never the cell:

```csharp
if (nodeBad.Contains(n)) continue;                   // permanent, cached
if (!nodeOk.Contains(n))
{
    if (!IsOpen(n, traversalRadius, true)) { nodeBad.Add(n); continue; }
    nodeOk.Add(n);
}
if (!HasClearEdge(cur, n)) continue;                 // edge only - do NOT blacklist n
if (!enqueued.Add(n)) continue;                      // enqueue-once bookkeeping
```

This can only ever *widen* the set of cells considered, never narrow it, so it cannot make any currently
working click fail. Note the full flood-fill in the region cache is immune to the bug by construction
(no greedy ordering, every cell reached from every direction), so the two changes reinforce each other.

**Verify.** Re-run the per-map click matrix in `docs/roles/apparater.md`, paying attention to clicks
just past doorways and around the Storage crate pile — the cases the bug should most affect.

---

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

**Why it went unnoticed.** `docs/roles/sentinel.md` states that `WinConditionMet()` is *"a byte-for-byte
copy of `GlitchRole.WinConditionMet()`, including the leftover local variable name `glitchCount`."* It
is not: it drops the `KillersAliveCount == 0` clause and swaps the player helper. The doc asserting
parity is exactly why nobody re-checked it. See SSA-022.

**DECIDED — match upstream.** Replace `Helpers.GetAlivePlayers()` with
`MiscUtils.GetImpactfulLivingPlayers()` in all four win conditions, keeping Pelican/Kirby's
devoured/swallowed subtraction on top, and restore the `|| MiscUtils.KillersAliveCount == 0` guard in
Sentinel and Gooper (SSA-005). Verified available in the pinned package:
`MiscUtils.GetImpactfulLivingPlayers()` is public static in
`~/.nuget/packages/townofusmira/1.7.3/lib/net6.0/TownOfUsMira.dll`. Also correct `sentinel.md`'s
"byte-for-byte" claim as part of the same change.

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

**The design doc asserts the opposite.** `docs/roles/pelican.md` justifies the exception explicitly:
*"this makes `WinConditionMet()` an exception to the 'pure predicate' pattern every other role's win
check follows; it's safe only because the digest is idempotent … **and it's called on every client that
evaluates the win check, not just host**."* The second half of that safety argument is false — the
patch above means only the host ever evaluates it. The stated precondition for the exception being safe
does not hold. See SSA-022.

**Impact.** Incorrect end-game summary on all non-host clients whenever a Pelican wins without a meeting.
Cosmetic rather than game-breaking, but it is a real host/client divergence, and both the code and the
doc read as if it were solved.

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

**Correction after reading `apparater.md`.** Two things I proposed already exist or are already planned,
and Phase 2 should not re-derive them:

- **It is already instrumented.** [`ApparaterMapButton.cs:159-179`](../SuperSquadAmongUs/Buttons/Crewmate/ApparaterMapButton.cs#L159-L179)
  times every search with a `Stopwatch` and logs the milliseconds on both the success and the
  no-reachable-point paths, alongside a distinct `Apparater: click ignored - <reason>` line for every
  rejection. `BepInEx/LogOutput.log` from any past session already distinguishes *slow* from
  *unreliable*. Read it before optimising.
- **The doc already names the next two steps**: *"order seeds by distance to the click first (nearly
  free — the frontier is already distance-keyed), and only then consider a bidirectional search (flood
  from the click and the seed side, stop when they meet). Don't build the latter speculatively."*

Note also that `MaxExpandedCells` is deliberate — `apparater.md` calls it out as the thing that stops a
cross-ship click hitching — so lowering it is a gameplay change (distant-but-reachable targets start
failing), not a free optimisation.

**DECIDED — cache the region per round** (see SSA-019 for the paired correctness fix). The key fact,
verified in code: traversal is **door-independent** — `IsOpen(..., ignoreDoors: true)` and
`HasClearEdge` both skip door colliders, and only the *landing* probe is door-aware. The connectivity
graph is therefore invariant for a whole round and can be flood-filled once by a coroutine spread over
frames, after which a click is a small local lookup plus one door-aware landing probe. This is *not* the
"pregenerated map of valid teleport points" rejected in round 6: that rejection was about total eager
work, and did not account for the graph being static, so the cost can be moved off the click entirely.
Two costs to design for: the memory for the cell set, and a fallback to the live search for a click that
arrives before the flood finishes.

Free win regardless: hoist `CollectDoorColliderIds()` out of the per-attempt loop in
`TryFindRandomReachablePoint` — it runs four `GetComponentsInChildren` sweeps per attempt and the door
set cannot change between attempts within one frame.

**Still unverified** — the ~130k–190k figure is a static worst-case bound, not a measurement. The
early-success break at line 179 may make the typical case far cheaper.

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

**Not a disagreement with the design.** `vulture.md` deliberately chose per-frame local arrow syncing
over RPCs ("arrows are a UI-only convenience … reducing network traffic") and that reasoning is sound.
The finding is about the *mechanism* — `FindObjectsOfType` — not the cadence or the locality.

**Suggested fix.** Dead bodies change rarely. Either poll on a slower cadence (every N ticks), or track
bodies via the existing `DeadBody` lifecycle (bodies are created on murder and destroyed at meeting
start / on clean), or reuse whatever body cache TOU-Mira already maintains if one exists. Behaviour to
preserve: arrows appear for new bodies and disappear when a body is reported, cleaned or eaten.

---

### SSA-023 — Four `ClickHandler` overrides bypass the keybind arbiter; every two-phase button's *second* phase is unguarded

**Severity:** Medium
**Location:** [`NinjaMarkButton.cs:101`](../SuperSquadAmongUs/Buttons/Impostor/NinjaMarkButton.cs#L101),
[`DetonatorAttachButton.cs:86`](../SuperSquadAmongUs/Buttons/Impostor/DetonatorAttachButton.cs#L86),
[`RcXdDeployButton.cs:106`](../SuperSquadAmongUs/Buttons/Impostor/RcXdDeployButton.cs#L106),
[`DumperCarryButton.cs:94`](../SuperSquadAmongUs/Buttons/Impostor/DumperCarryButton.cs#L94)

**Evidence.** Every `ClickHandler` override in the addon, brace-matched and checked:

| Override | Claims the keybind? |
|---|---|
| `SuperSquadRoleButton<TRole>` / `<TRole,TTarget>`, `GrantedTargetButtonBase`, `GrantedSwoopButton` | yes |
| `DaddyHagridHideButton`, `InvisibilityCloakButton`, `SlideTackleButton` | yes |
| **`NinjaMarkButton`** | **no** — never calls `base.ClickHandler()`; both phases unguarded |
| **`DetonatorAttachButton`** | **no** — never calls `base.ClickHandler()`; both phases unguarded |
| **`RcXdDeployButton`** | **partly** — deploy goes through `base`, the *detonate* branch returns first |
| **`DumperCarryButton`** | **partly** — store goes through `base`, the *early-dump* branch returns first |

The pattern is systematic rather than four separate slips: `base.ClickHandler()` is what claims, so any
branch that returns before reaching it is unarbitrated — and in a two-phase button that is always the
second phase (detonate, dump, assassinate).

**`DaddyHagridHideButton` is the counterexample that proves it's a defect, not a choice.** It is the same
two-phase shape as `DumperCarryButton` — same `ToggleDebounce`, same
`if (EffectActive) { if (!CanUse()) return; ResetCooldownAndOrEffect(); }` — and it *does* claim, with a
comment saying why:

```csharp
// KeybindArbiter check here too: this branch bypasses base.ClickHandler()'s CanClick() gate
// entirely, so without it a shared PrimaryAction press could release early AND let another
// grant-holder button (e.g. GrantedKillButton) fire uncontested on the same keypress.
if (!CanUse() || !KeybindArbiter.TryClaim(Keybind))
```

`DumperCarryButton` reads as a copy of that method with the arbiter term dropped.

**Why this is likely to bite.** A keybind census shows **ten** buttons on `SecondaryAction` — Elusive,
Sui retaliate, Astral, **Detonator**, **Dumper**, Eraser, MafiaJanitor, **Ninja**, **RC-XD**, Sniper,
Witch. `gooper.md` notes borrowed kits keep their source keybind, so an accumulate-mode Kirby holding
two Secondary kits is an ordinary outcome, and all four affected buttons are on Secondary.

**Failure scenario.** A Kirby has swallowed a Ninja and a Sniper. One Secondary press: `NinjaMarkButton`
fires unarbitrated *and* `SniperSnipeButton` claims the frame and arms the aim — the player marks a
target and enters sniper aim on a single keypress. Same for a Dumper mid-carry who also holds another
Secondary ability: one press dumps the body *and* fires the other kit.

**Impact.** Exactly the double-fire `KeybindArbiter` exists to prevent, and `rc-xd.md` records having
already shipped a "one press did two things" bug once.

**Suggested fix.** Add `|| !KeybindArbiter.TryClaim(Keybind)` to the guard in each unguarded branch,
after the existing can-fire check (the arbiter's own remarks require claiming only once the press will
actually be honoured). Four edits; `DaddyHagridHideButton` is the reference implementation.

Keep the existing `ToggleDebounce` / `DetonateArmDelay` guards — those defend against one *physical*
press dispatching twice (autorepeat), which is a different problem from two *different* buttons sharing
a keybind, and the arbiter's per-(keybind, frame) claim would not cover it.

**Verify.** Playtest: as a Kirby holding two Secondary kits, one press must fire exactly one ability —
in both phases of each two-phase button.

---

### SSA-024 — `NinjaMarkButton` uses the bare `DisabledModifier` presence check the docs warn against

**Severity:** Medium
**Location:** [`Buttons/Impostor/NinjaMarkButton.cs:105-106`](../SuperSquadAmongUs/Buttons/Impostor/NinjaMarkButton.cs#L105-L106)

**Evidence.**

```csharp
if (!CanClick() || PlayerControl.LocalPlayer.HasModifier<GlitchHackedModifier>() ||
    PlayerControl.LocalPlayer.HasModifier<DisabledModifier>())     // <-- bare presence check
```

`docs/roles/sniper.md` states the rule explicitly: *"The disabled check must use
`GetModifiers<DisabledModifier>().Any(x => !x.CanUseAbilities)`, not a bare
`HasModifier<DisabledModifier>()` — some subclasses (`GrenadierFlashModifier`, `EclipsalBlindModifier`)
set `CanUseAbilities = true` to explicitly opt out of blocking abilities."*

A repo-wide grep confirms this is the **only** remaining bare gate; the other nine call sites
(`SniperSnipeButton`, `RcXdDeployButton`, `DetonatorAttachButton`, `DumperCarryButton`,
`DaddyHagridHideButton`, `SlideTackleButton`, `GrantedAbilityButtons`, `SniperShots`,
`SuperSquadDetonator`, `RcXdCar`) all use the correct opt-out-respecting form.

**Failure scenario.** A Ninja flashed by a Grenadier or blinded by an Eclipsal cannot mark or
assassinate, even though both modifiers explicitly declare they do not block abilities. The Ninja is
silently more punished by those effects than every other role in the addon.

**Impact.** Over-blocking: the ability is denied in states where it should work. Note this errs safe —
it never lets a disabled player act — which is why it has gone unnoticed.

**Suggested fix.** One-line change to the documented form. Consider pairing with SSA-023 since both are
in the same guard.

---

### SSA-025 — RC-XD computes "does the deployer die?" twice, and the two can disagree

**Severity:** Medium
**Location:** [`Buttons/Impostor/RcXdDeployButton.cs:160-173`](../SuperSquadAmongUs/Buttons/Impostor/RcXdDeployButton.cs#L160-L173)
vs [`Modules/RcXdCar.cs:105-133`](../SuperSquadAmongUs/Modules/RcXdCar.cs#L105-L133)

**Evidence.** The button decides whether to skip the camera linger:

```csharp
var deployerInBlast = options.CanKillImpostors &&
    Helpers.GetClosestPlayers(new Vector2(carPosition.x, carPosition.y), radius).Any(p => p.AmOwner);
```

The RPC independently decides who actually dies, with a different and longer filter:

```csharp
if (player == null || player.Data == null || player.Data.IsDead || player.Data.Disconnected || player.inVent) continue;
if (!options.CanKillImpostors && player.IsImpostorAligned()) continue;
if (player.HasModifier<FirstDeadShield>() ||
    player.GetModifiers<DisabledModifier>().Any(x => !x.CanBeInteractedWith)) continue;
```

Two copies of the same question, sharing no code. They diverge in at least three reachable cases:

1. **Borrowed kit + `CanKillImpostors` off.** `RcXdRole`'s kit is transferable, so a Kirby can drive the
   car. The button short-circuits on `options.CanKillImpostors` → `deployerInBlast == false` → it takes
   the **linger** branch. But the RPC's skip rule is `!CanKillImpostors && IsImpostorAligned()`, and a
   Kirby is **not** impostor-aligned — so the Kirby **is** killed. The button then restores camera and
   light onto a player mid-death-teardown, which is precisely the situation the linger-skip exists to
   avoid (`rc-xd.md`: *"restore fully while still alive and skip the linger instead of restoring onto a
   ghost mid-death-teardown"*). This is the 2026-07-17 "camera stuck at the blast site" bug's
   preconditions, reachable again through a borrowed kit.
2. **`FirstDeadShield` holder.** Button says in-blast → skips the linger; RPC filters them out → they do
   not die. The deployer survives but loses the explosion camera for no reason.
3. **`DisabledModifier` with `CanBeInteractedWith == false`, or `inVent`.** Same shape as (2).

**Impact.** Case 1 is the real one: a wrong branch during death teardown, on the exact code path the
role's history says was hard to get right. Cases 2–3 are cosmetic.

**Suggested fix.** Have one function answer the question. Extract the victim filter from
`RcXdCar.RpcDetonateCar` into a shared helper (e.g. `RcXdCar.GetBlastVictims(position)`) and let the
button ask `victims.Any(p => p.AmOwner)` rather than re-deriving it. That removes the divergence by
construction instead of patching three cases.

**Verify.** Playtest case 1 specifically: Kirby swallows an RC-XD, `CanKillImpostors` off, deploy and
detonate on top of yourself — control and camera must return cleanly.

---

### SSA-026 — `WitchHexButton` keeps cast state but neither survives death nor self-heals

**Severity:** Medium
**Location:** [`Buttons/Impostor/WitchHexButton.cs:24-26`](../SuperSquadAmongUs/Buttons/Impostor/WitchHexButton.cs#L24-L26),
[`:57-71`](../SuperSquadAmongUs/Buttons/Impostor/WitchHexButton.cs#L57-L71)

**Evidence.** The button holds a multi-tick channel: `castTarget`, `EffectActive`, and
`CurrentCooldownAddition`. It does **not** override `Enabled`.

`docs/il2cpp-gotchas.md` §"Role-gated buttons stop ticking the moment the player dies": MiraAPI only
drives `FixedUpdate` while `Enabled(role)` is true, and death swaps `Data.Role` to a ghost role. Every
other effect-holding button in the addon that owns local state overrides `Enabled` to keep ticking —
`SniperSnipeButton`, `RcXdDeployButton`, `DumperCarryButton`, `DaddyHagridHideButton`,
`ApparaterMapButton`, `SlideTackleButton`, `InvisibilityCloakButton`, `SuiRetaliateButton`. Witch is the
one that does not.

I checked the other two effect buttons lacking the override and they are **fine**: `ElusiveShieldButton`
and `AstralFormButton` hold no local fields — their state lives on modifiers, which tick independently
of the button.

**Failure scenario.** A Witch dies mid-channel (a Sheriff misfire, or hexing an alerted Veteran).
`FixedUpdate` stops, so neither `CancelCast()` nor `OnEffectEnd()`→`CompleteCast()` ever runs;
`castTarget` and `EffectActive` are left set on a singleton that, per the same doc, *"persists for the
whole game process"*. `WitchEvents` resets only `CurrentCooldownAddition` — nothing clears `castTarget`.

**Impact.** At minimum the channel never resolves and the hex is silently lost. The cross-game leak —
a pending `EffectActive` completing against a stale `PlayerControl` from a previous lobby — is the worse
case; `CompleteCast()`'s `castTarget == null || castTarget.HasDied()` guard mitigates it, but a
destroyed Il2Cpp reference is not a safe thing to call `HasDied()` on.

**Confidence.** The "cleanup never runs on death" half is certain from the control flow plus the
documented gotcha. The cross-game half is **unverified** — it depends on MiraAPI's exact handling of
`EffectActive` when a button is re-enabled in a later game, which I did not trace.

**Suggested fix.** Two lines, mirroring what the other buttons already do: override `Enabled` to stay
true while `EffectActive`, and add a `FixedUpdate` self-heal that cancels the cast when the caster has
died or a meeting has started. Consider also clearing `castTarget` in `ResetCooldownAddition()` (rename
it) so the existing game-start hook covers everything.

---

### SSA-020 — `VultureRole.EatenBodies` breaks the documented "state on the button singleton" rule

**Severity:** Medium
**Location:** [`Roles/Neutral/VultureRole.cs:62`](../SuperSquadAmongUs/Roles/Neutral/VultureRole.cs#L62),
[`Modules/SuperSquadBodies.cs:58-61`](../SuperSquadAmongUs/Modules/SuperSquadBodies.cs#L58-L61)

**Evidence.** `docs/roles/gooper.md` states the rule as verified: *"Three rules keep buttons borrowable
(all verified by audit): per-ability mutable state lives on the BUTTON singleton, never on the role
class…"*. I re-ran that audit. Two role classes hold per-ability mutable state:

```
GooperRole.GoopedBodyIds   (see SSA-021 - latent only, Gooper's kit never transfers)
VultureRole.EatenBodies    (live violation - VultureRole IS kit-transferable)
```

`AbilityGrants.KitExcludedRoles` contains only `GodfatherRole` and `MafiosoRole`, so swallowing a
Vulture adds `VultureRole` to a Kirby's `GrantedKits`, and `VultureEatButton` — a
`SuperSquadRoleButton<VultureRole, DeadBody>` — becomes enabled for it. The RPC then splits:

```csharp
// SuperSquadBodies.cs:48  - passes for a borrower
if (!AbilityGrants.SenderIsOrHolds<VultureRole>(source)) { ... return; }
// SuperSquadBodies.cs:58  - fails for a borrower
if (DestroyBodies(parentId) && source.Data.Role is VultureRole vulture) { vulture.EatenBodies++; }
```

**Failure scenario.** A Kirby swallows a Vulture, then eats bodies. Each eat destroys the body on every
client and credits nobody. Worse, `kirby.md` advertises kit inheritance as universal ("swallow an
Apparater → real Apparate, an RC-XD → the real car … automatic for every current and future role here"),
so this reads as a working inherited ability. If a second Vulture is alive, the Kirby is silently
destroying the bodies that Vulture needs to win, with no benefit to itself.

**Impact.** A borrowed ability that is useless to its holder and actively harmful to another role's win
condition — a griefing tool reachable through normal play.

**DECIDED — exclude Vulture from kit transfer.** Add `typeof(Roles.Neutral.VultureRole)` to
`AbilityGrants.KitExcludedRoles` alongside Godfather/Mafioso, and extend that field's comment to say
why: the eat ability is meaningless without the per-Vulture counter its win is measured on. Note this in
`vulture.md` and in `kirby.md`'s list of deliberate exceptions, both of which currently imply the
opposite.

---

### SSA-022 — Two design docs assert invariants the code does not hold

**Severity:** Medium
**Location:** `docs/roles/sentinel.md`, `docs/roles/pelican.md`

**Evidence.** Both state a specific, checkable property as settled fact, and both are wrong. Each
false claim is load-bearing: it is the reason the underlying defect went unexamined.

| Doc | Claim | Reality |
|---|---|---|
| `sentinel.md` | `WinConditionMet()` is *"a byte-for-byte copy of `GlitchRole.WinConditionMet()`"* | It drops `\|\| MiscUtils.KillersAliveCount == 0` and swaps `GetImpactfulLivingPlayers()` → `GetAlivePlayers()` (SSA-001, SSA-005) |
| `pelican.md` | the in-predicate digest *"is called on every client that evaluates the win check, not just host"* | `CheckEndCriteria` is host-gated, so only the host ever evaluates it (SSA-003) |

**Impact.** This is the most consequential *process* finding in the review. `CLAUDE.md` instructs future
sessions to read the role doc before touching a role, so a doc that certifies a property nobody
re-checked converts a one-time mistake into a permanent blind spot. Both defects survived a version
upgrade and this review's first pass for exactly that reason.

**Suggested fix.** Correct both statements as part of the code fixes they describe (SSA-001, SSA-003),
rather than as a separate docs pass — the doc and the code should change in the same commit. More
generally, worth preferring "intended to match X" over "is a byte-for-byte copy of X" in these docs
unless the equivalence is actually enforced by something.

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

**CORRECTION — do not delete.** `docs/roles/sniper.md` records this as a dated user decision, not an
oversight: *"**Bullet visual removed entirely.** User decision 2026-07-16 … `SniperShots.RpcShowShot` /
`ShowShotLocally` are KEPT as reusable machinery for future roles that copy the shot logic but want a
visible projectile."* The same doc explains the aim guide's removal, which is why `SniperGuideSprite`
is also retained deliberately. My Phase 1 recommendation to delete all of it was wrong.

**Revised fix.** Keep the machinery; close the one real defect — add the missing
`AbilityGrants.SenderIsOrHolds<SniperRole>(source)` guard to `RpcShowShot`, matching the other ten RPCs,
so a retained-for-later code path is not also an unvalidated network entry point any client can spam.
Optionally add a one-line comment on `SuperSquadRpc.ShowSniperShot` noting it is intentionally unused,
so the next reviewer does not re-file this.

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

**Evidence.** `sentinel.md` already tracks this as a known follow-up ("Unrenamed copy-paste residue …
confirmed, no functional bug") and lists more of it than my first pass found. Full verified set:

| Residue | Location | From |
|---|---|---|
| `glitchCount` | `SentinelRole.cs:73` | TOU-Mira Glitch |
| `douse` (holds the *explode* button), `ignite` (holds the *kill* button) | `SentinelRole.cs:86-87` | Arsonist |
| `dousedPlayers` (Sentinel has no douse step) | `SentinelExplodeButton.cs:68,69,77` | Arsonist |
| `igniteRadius`, local `ignite` | `Explode.cs:21,23,26,31` | Arsonist |
| `TouAudio.ArsoIgniteSound`, `AuAvengersAnims.IgniteMaterial` | `SentinelExplodeButton.cs:80`, `Explode.cs:24` | Arsonist (assets, not just names) |

**Impact.** Misleading to a reader; `douse`/`ignite` in `OffsetButtons` actively name the wrong
abilities. The two borrowed Arsonist assets are a cosmetic follow-up, not a rename.

**Suggested fix.** Rename the identifiers (`sentinelCount`, `explodeButton`, `killButton`,
`playersInRange`, `explosionRadius`, `explosion`). Pure rename, no behaviour change. The Arsonist sound
and material are an art/audio task — leave them, they are already tracked in `sentinel.md`.

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

Together with the two CA1310s in SSA-008, these five are the entire non-CS1591, non-false-positive
warning surface of the build — the signal-to-noise here is genuinely good once doc-comment warnings are
set aside.

---

### SSA-021 — `GooperGoopButton` dereferences `Role` in four places, which is null for a borrower

**Severity:** Low (latent)
**Location:** [`Buttons/Neutral/GooperGoopButton.cs:50,55,68,110`](../SuperSquadAmongUs/Buttons/Neutral/GooperGoopButton.cs#L50)

**Evidence.** Four call sites read `Role.GoopedBodyIds`, two of them on per-frame paths (`GetTarget`,
the arrow sync). `gooper.md` gives "their `Role`-typed buttons NRE for a borrower" as one of the reasons
TOU-Mira's buttons cannot be Layer 1 — our own button has the same shape.

It is safe **today** purely because `ApplyPortableGrant` routes a victim that is itself an
`IAbilityGrantHolder` down a branch that never adds its kit:

```csharp
if (victimRole is IAbilityGrantHolder victimHolder)   // Gooper and Kirby take this branch
{
    recipient.UnlockedAbilities |= victimHolder.UnlockedAbilities;
    recipient.GrantedKits.UnionWith(victimHolder.GrantedKits);   // never adds victimRole itself
}
```

So `GooperRole` can never enter anyone's `GrantedKits`, and `Enabled` gates the button to real Goopers.

**Impact.** None currently. It is a tripwire: adding `GooperRole` to `AbilityGrants.Pool`, or making
grant-holders transfer their own kit, produces an NRE inside `GetTarget()` — which runs every frame —
with nothing in the code stating the dependency.

**Suggested fix.** Cheapest is a comment at the `Role.` sites (and on `GoopedBodyIds`) recording that
this button is safe only because Gooper's kit is non-transferable. Better, and consistent with the rule
`gooper.md` states: move the gooped-body set onto the button singleton the way `SuiProtectButton`'s
`protectedTarget` already was — the doc cites that as the worked example of exactly this migration. Not
urgent; do it if Gooper is touched for other reasons.

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
- **The six CA1707 warnings** (`Patches/InvisibleBoyAdminPatch.cs:35`,
  `InvisibleBoyVisibilityPatch.cs:20`, `LogoPatch.cs:14`, `MafiaLabelsPatch.cs:44`,
  `MafiosoGatePatches.cs:46`, `WitchMeetingPatch.cs:20`). All are "remove the underscores from
  parameter name `__instance`/`__result`". Harmony *requires* those exact names to inject the patched
  instance and return value — renaming them silently breaks the patch. Suppress or ignore; never "fix".
- **`SightChecker.CanAnyoneSee` cost.** It looked like a hot path (`PlayerControl.FixedUpdate` postfix for
  every player), but the raycast is short-circuited behind a cheap distance test, and the patch filters to
  `InvisibleBoyRole` before doing any work.
