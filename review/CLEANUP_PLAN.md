# Phase 2 Cleanup Plan

Ordered so each stage can land independently. Later stages assume earlier ones are in, but nothing in
Stage 1–4 blocks anything in Stage 5 except where explicitly flagged.

IDs refer to [FINDINGS.md](FINDINGS.md).

---

## Precondition — settle the working tree first

The review was performed against a working tree carrying a large **uncommitted** change set (the
TOU-Mira 1.7.1→1.7.3 / MiraAPI 0.4.3→0.5.0 upgrade, 86 modified files). Every line number in
FINDINGS.md is against that state.

**Before any Phase 2 work:** commit or otherwise land that upgrade. Starting cleanup on top of an
uncommitted 86-file diff makes every subsequent change impossible to review in isolation, and a bad
`git checkout` would destroy work that is not yet in history.

---

## How anything gets verified here

There is no automated test suite, and there cannot easily be one — this is a client-side IL2CPP Unity
mod, not a library. `CLAUDE.md` is explicit that a successful `dotnet build` is the primary automated
check and real behaviour needs a running game.

That shapes this whole plan: it is ordered **most-mechanically-verifiable first**. Two verification
tiers are used below.

- **Build-verified** — `./scripts/build.sh` succeeds and the warning profile does not regress. Capture
  a baseline first:
  ```
  ./scripts/build.sh 2>&1 | grep -oE 'warning [A-Z]+[0-9]+' | sort | uniq -c | sort -rn > review/warning-baseline.txt
  ```
  Current baseline: `1448 CS1591`, `12 CA1707`, `4 S125`, `4 CA1310`, `2 CS8618`, `0 errors`. Several
  stages below should *reduce* specific counts; nothing should introduce a new code.
- **Playtest-verified** — needs a running Among Us + BepInEx, and for some items two clients. Each item
  below names the specific thing to look at. Deploy with `./scripts/deploy.sh`.

**Recommendation:** write down the Stage 5 playtest scenarios as a checklist in `docs/roles/<name>.md`
before changing that role, so the "before" behaviour is recorded while it is still observable. Several
role docs already have a *Playtest checklist* section (see `docs/roles/rc-xd.md`) — follow that shape.

---

## Stage 1 — Deletions and renames with no behaviour change

All independent of each other. Each is its own commit.

### 1.1 Delete the Sniper's dead projectile-visual machinery (SSA-010, SSA-014)

**What changes.** Remove `RpcShowShot`, `ShowShotLocally`, `CoBulletTravel`, the `VisualRange` /
`VisualSpeed` constants from `Modules/SniperShots.cs`; remove `SuperSquadRpc.ShowSniperShot`; remove
`SuperSquadImpAssets.SniperGuideSprite` and `Resources/ImpButtons/SniperGuide.png`. Rewrite the class
comment, which currently documents both the dead machinery and (incorrectly) describes `FindHits` as an
"infinite line" — correct it to a forward ray while in there.

**Why.** Zero callers. It is also the one RPC of eleven with no sender validation and no comment
explaining the omission, so deleting it closes an unvalidated network entry point for free.

**Risk.** Very low. The only subtlety is the RPC enum: leaving a gap in `SuperSquadRpc` is harmless
(Reactor scopes ids per plugin and they only need to be unique within this addon), so **do not
renumber** the remaining entries — renumbering would break compatibility with any client running an
older build during a mixed-version lobby.

**Verify.** Build-verified. Grep confirms zero references before deleting. Playtest: fire the Sniper
once and confirm the shot still kills — nothing visual should change, because nothing visual was ever
rendered.

**Depends on:** nothing.

### 1.2 Delete unused declarations (SSA-012)

**What changes.** Remove `SuperSquadColors.Chameleon` and `SuperSquadNeutAssets.VultureArrowSprite`
(and `VultureArrow.png` if nothing else references it).

**Why.** Zero references; no Chameleon role exists.

**Risk.** Very low. Check the PNG is not referenced from a locale file or doc before deleting the image.

**Verify.** Build-verified.

**Depends on:** nothing. ⚠️ **Do not** delete the unused `SuperSquadRoleIcons` members here — six of
them are art that should be *wired up* in 2.3, not removed.

### 1.3 Rename copy-paste identifiers in SentinelRole (SSA-013)

**What changes.** `glitchCount` → `sentinelCount`; `douse` → `explodeButton`; `ignite` → `killButton`.

**Why.** They are named after TOU-Mira's Glitch and Arsonist, and `douse`/`ignite` are additionally
attached to the wrong abilities relative to their names.

**Risk.** None — local variables only.

**Verify.** Build-verified.

**Depends on:** nothing. ⚠️ Touches `SentinelRole.WinConditionMet`, the same method as 5.1/5.2 — land
this **before** those, or expect a trivial conflict.

### 1.4 Remove commented-out code (SSA-018)

**What changes.** Delete the commented-out blocks at `Buttons/Impostor/WitchHexButton.cs:111` and
`Events/EraserEvents.cs:78`.

**Why.** Flagged by SonarAnalyzer (S125); git history preserves them.

**Risk.** None, provided the comments are genuinely dead code and not a deliberate "why not" note. Read
each before deleting — this codebase uses explanatory comments heavily and one of these may be
documenting a rejected approach, in which case rewrite it as prose instead of deleting.

**Verify.** Build-verified; `S125` count should drop from 4 to 2 (2 remain elsewhere).

**Depends on:** nothing.

---

## Stage 2 — Mechanical correctness fixes

### 2.1 Ordinal string comparison in MafiaLabelsPatch (SSA-008)

**What changes.** `EndsWith(tag)` → `EndsWith(tag, StringComparison.Ordinal)` at
`Patches/MafiaLabelsPatch.cs:35` and `:60`.

**Why.** The patch's idempotence depends entirely on this comparison, and the culture-sensitive overload
can fail to match under some cultures, causing the tag to be re-appended every frame.

**Risk.** Very low — ordinal is strictly more predictable and is the correct comparison for a literal
marker.

**Verify.** Build-verified; `CA1310` count drops 4 → 2. Playtest: as a mafia member, confirm teammates'
names show exactly one `(G)`/`(M)`/`(J)` suffix, in-world and in a meeting, and that it does not grow
over time.

**Depends on:** nothing.

### 2.2 Fix `Explode.Transform` nullability (SSA-018)

**What changes.** `Modules/Explode.cs` — the class is only ever constructed through `CreateExplode`,
which always assigns `Transform`. Give it a constructor taking the transform (or `required init`).

**Why.** Silences CS8618 and makes the invariant explicit.

**Risk.** Low. `Clear()` dereferences `Transform.gameObject`; confirm `SentinelExplodeButton`'s
`Explode` field handling still compiles (it assigns and nulls it).

**Verify.** Build-verified; `CS8618` drops 2 → 1. Playtest: Sentinel explode still renders and clears.

**Depends on:** nothing.

### 2.3 Wire up the six unused role icons (SSA-002)

**What changes.** Add `Icon = SuperSquadRoleIcons.<Name>,` to `Configuration` in `WitchRole`,
`VultureRole`, `GodfatherRole`, `MafiosoRole`, `MafiaJanitorRole`, `EraserRole`. For `AstralRole`,
`NinjaRole`, `SniperRole`, `PelicanRole`, `RcXdRole` — decide per role whether to wire the placeholder
explicitly (as `DetonatorRole`/`DumperRole`/`KirbyRole` already do) or delete the member.

**Why.** Six finished PNGs are embedded in the DLL and never displayed.

**Risk.** Low, but this **is** a visible behaviour change — six roles will look different in the role
menu, guide and wiki. Check each icon renders at a sane size; `docs/icon-standards.md` documents the
200 PPU convention for role icons, and all six already declare `200`.

**Verify.** Playtest-verified — open the role settings menu and the in-game role guide and confirm each
of the six shows its own art rather than the generic team icon. This is a look-at-it check; no build
signal will catch a wrong-looking icon.

**Depends on:** nothing, but do it **before** any cleanup that deletes "unused" icon members, or the art
gets thrown away instead of connected.

### 2.4 Remove per-frame allocation in the Mafioso gate (SSA-017)

**What changes.** `Patches/MafiosoGatePatches.cs` — replace `Helpers.GetAlivePlayers().Any(...)` with a
non-allocating scan over `PlayerControl.AllPlayerControls`.

**Why.** Allocates a `List<PlayerControl>` per `HudManager.Update` frame for a local Mafioso.

**Risk.** Low, but note the two helpers are **not** equivalent: `GetAlivePlayers()` iterates
`GameData.Instance.AllPlayers` and filters `!IsDead && !Disconnected && Object`. A replacement must apply
the same filters or the gate will behave differently for disconnected Godfathers. Match the filter
exactly.

**Verify.** Build-verified. Playtest: as Mafioso with a living Godfather, kill and sabotage buttons are
hidden; when the Godfather dies mid-round both reappear immediately without a hud refresh.

**Depends on:** nothing.

---

## Stage 3 — Localization sweep (SSA-007)

**What changes.** Convert hardcoded English display strings to locale lookups:
19 `ModifierName` overrides, 8+ `GetDescription()` bodies in `Modifiers/`, and
`SuperSquadModifierOptions.GroupName`. Add the corresponding `SuperSquad*` keys to
`Resources/Locale/en_US.xml`.

**Why.** Roles are fully localized and the addon ships 20 locale files; modifiers are not covered at all,
so no translation can reach them without a code change.

**Risk.** Low **if done with fallbacks**. Use the two-argument form, passing the current literal as the
fallback:

```csharp
public override string ModifierName => MiraLocaleManager.Get("SuperSquadModifierDevoured", "Devoured");
```

That makes each conversion behaviour-preserving even before the key exists in the XML, so the change is
safe to land in pieces. Two constraints:

- Keep the `SuperSquad*` key prefix. A duplicate key against TOU-Mira's ~2,900 keys logs an error and
  **silently overwrites**, last-registered-wins.
- Locale XML uses `[b]` / `[nl]` bracket syntax, not the old `\%b\%` form — the bracket→angle conversion
  happens at load time in `MiraLocaleManager.ParseXmlFile`.

**Verify.** Build-verified for compilation. Then a key-coverage check — every key the code builds should
exist in `en_US.xml`:

```
# adapt the cross-check already used during review: extract MiraLocaleManager.Get("...") literals
# and diff against name="..." attributes in Resources/Locale/en_US.xml; expect zero missing
```
Playtest: trigger a few modifiers (devour, hex, tackle) and confirm the modifier UI text is unchanged
from today.

**Depends on:** nothing. Best done as one focused commit per folder so a bad key is easy to bisect.

---

## Stage 4 — Behaviour-preserving refactors

### 4.1 Extract the shared blank-appearance builder (SSA-015)

**What changes.** Replace the three byte-identical `GetVisualAppearance()` bodies in `CarriedModifier`,
`TimedInvisibilityModifier` and `InvisibleBoyModifier` with calls to one shared helper. Same for the
three near-identical `ApplyLocalVisibility()` implementations.

**Why.** Three copies of a recipe (hat/skin/visor/pet/name ids) that an upstream change could invalidate
in all three at once.

**Risk.** Medium — this is the most delicate item in Stages 1–4, because appearance handling is exactly
where this codebase has historically been bitten.

- The appearance type **must** stay `TownOfUsAppearances.Swooper`. `InvisibleBoyModifier`'s comment
  records that this is deliberate: it piggybacks on TOU-Mira's comms-camouflage skip for that
  appearance. Changing it would silently un-hide players during comms sabotage.
- `LocalViewerSeesOutline()` genuinely differs between the three (Carried = owner + informed dead;
  TimedInvisibility = owner + fellow impostors + informed dead, and is already `virtual`). The helper
  must take the computed boolean as a parameter, not re-derive it.
- The three classes sit on different bases, so the helper should be a static utility rather than a new
  shared base class.

**Verify.** Build-verified, plus a careful diff review that the extracted body is character-identical to
what it replaced. Playtest: Invisible Boy, Astral phase, and a devoured player each render correctly
for (a) themselves, (b) a living observer, (c) a fellow impostor, (d) a dead observer with "the dead
know" on — that matrix is what the three variants encode.

**Depends on:** nothing. ⚠️ Do **not** bundle with Stage 3 — if a localization change and an appearance
refactor land together, a rendering regression is hard to attribute.

### 4.2 Collapse the KeybindArbiter click boilerplate (SSA-016)

**What changes.** Add a small helper encapsulating the `CanClick() + TryClaim(Keybind)` pair; call it
from the four `ClickHandler` overrides.

**Why.** Four copies of a guard with a documented sharp edge (`TryClaim` mutates state and must never be
called from a predicate).

**Risk.** Low-medium. The fourth copy (`GrantedSwoopButton`) is **not** identical — it uses `CanUse()`
rather than `CanClick()` and deliberately bypasses `base.ClickHandler()` because it is a toggle. Either
leave it alone or give the helper an explicit overload; do not flatten it into the same call by
accident.

**Verify.** Build-verified. Playtest: as a Gooper or Kirby holding two abilities on the same keybind,
press it and confirm exactly one fires — this is the whole point of the arbiter.

**Depends on:** nothing.

### 4.3 De-duplicate and de-hot-path the body arrows (SSA-009)

**What changes.** `VultureEatButton` and `GooperGoopButton` share ~50 lines of arrow machinery
(`bodyArrows`, `SyncArrows`, `ClearArrows`, `DestroyArrow`), each calling
`FindObjectsOfType<DeadBody>()` every `FixedUpdate`. Extract the shared machinery and stop doing a full
scene scan at 50 Hz.

**Why.** `FindObjectsOfType` is a full scene-graph scan plus an array allocation, 50× per second for the
life of a Vulture or Gooper.

**Risk.** Medium — the two are *not* interchangeable: Gooper filters out already-gooped bodies, Vulture
does not, and `VultureEatButton` additionally clears arrows in `SetActive` when the role is no longer a
Vulture. Any shared implementation must keep both behaviours. If reducing the polling cadence, the
observable contract to preserve is: an arrow appears when a body appears, and disappears when that body
is reported, cleaned or eaten — a slower cadence makes that lag, which may be acceptable but is a design
call.

**Verify.** Playtest-verified, and it needs a second player to make bodies: confirm arrows appear for new
bodies, vanish on report/eat/clean, and do not survive the Vulture's death or a meeting.

**Depends on:** nothing, but it is the lowest-value item in Stage 4 — consider deferring unless profiling
(see 5.4) shows scene scans matter.

---

## Stage 5 — Behavioural fixes needing a design decision or measurement

These change what happens in a game. None should be started without the sign-off noted.

### 5.1 + 5.2 Neutral win conditions (SSA-001, SSA-005) — **do together**

**What changes.** In `SentinelRole`, `GooperRole`, `KirbyRole`, `PelicanRole`: swap
`Helpers.GetAlivePlayers()` for `MiscUtils.GetImpactfulLivingPlayers()`, keeping the existing
devoured/swallowed subtractions. In `SentinelRole` and `GooperRole` additionally restore the
`|| MiscUtils.KillersAliveCount == 0` guard.

**Why.** The upstream roles these were ported from (Glitch, Arsonist) exclude Neutral Benign players from
the count; ours do not, so the wins fail to trigger in the end-game states they exist for.

**⚠️ Needs a design decision first.** The divergence is consistent across four roles, which is weak
evidence it might have been deliberate. Confirm the intended rule: *should a living Survivor/Jester
prevent a Neutral Killing win?* Upstream says no. If the answer is "no", apply the fix; if "yes", instead
correct the code comments, which currently cite Glitch/Arsonist parity that does not hold.

**Risk.** High blast radius by nature — this makes four neutral roles win *more readily*. Expect the
change to surface as "the round ended earlier than it used to".

**Verify.** Playtest-verified, multi-client. Scenario: Sentinel + 2 Survivors + no other killers → the
game should end with a Sentinel win. Re-run the equivalent for Gooper, Kirby and Pelican. Also confirm no
premature win in a normal mid-game state with crew alive.

**Depends on:** land **after** 1.3 (same method, trivial conflict otherwise).

### 5.3 Rework the forced stomach digest (SSA-003, SSA-004) — **do together**

**What changes.** Remove the `DigestStomach()` side effect from `PelicanRole.WinConditionMet()` and
replace it with a mechanism that runs on every client — either an RPC broadcast by the host, or a hook on
game-over that fires everywhere. Apply the same mechanism to Kirby, which currently has no forced digest
at all.

**Why.** The only caller of `WinConditionMet()` is host-gated (`LogicGameFlowPatches.cs:188` returns
early for non-hosts), so the digest never runs on other clients — the end-game summary disagrees between
host and clients. A predicate that is polled every tick is also the wrong place for a state mutation.

**Risk.** Medium-high. `DigestStomach()` kills players; moving when it runs changes who dies and when. The
current implementation is idempotent and relies on that (it is invoked twice per evaluation today, once
from `IsMet` and once from `TriggerGameOver`) — preserve idempotence.

**Verify.** Playtest-verified, **two clients, non-host Pelican**: devour to the win threshold without
calling a meeting, then compare the end-game summary on host and client — devoured players must show as
dead on both. Repeat for Kirby. Also re-check the ordinary path (digest at a normal meeting) still works.

**Depends on:** should land after 5.1/5.2, since those change when the win threshold is reached and you
want to test one variable at a time.

### 5.4 Measure, then optimise, the reachability search (SSA-006)

**What changes.** First **measure** — instrument `WalkableRegionSolver.TryFindReachablePoint` with a
stopwatch, log elapsed time and the final `expanded` count, and exercise both real call paths
(Apparater map click, Elusive shield trigger) on the largest map. Only then optimise.

**Why.** The static worst case is ~130k–190k physics queries in one synchronous frame, and
`TryFindRandomReachablePoint` runs the whole search up to three times plus re-collects door colliders per
attempt. Whether that is a real hitch or a theoretical bound is unknown — the early-success break may
mean the typical case is cheap.

**If measurement justifies it**, in increasing order of risk:
1. Hoist `CollectDoorColliderIds()` out of the per-attempt loop in `TryFindRandomReachablePoint` (pure
   win, no behaviour change — the door set cannot change between attempts within one frame).
2. Memoise `IsOpen` per cell: the same cell centre is probed once as a traversal neighbour and again as a
   landing candidate.
3. Lower `MaxExpandedCells`. ⚠️ This *does* change behaviour — it makes distant-but-reachable targets
   fail. Only with a design decision.

**Risk.** Item 1 is safe. Item 2 needs care: `IsOpen` is called with two different radii
(`traversalRadius` and `landingRadius`) and two different `ignoreDoors` values, so any cache must key on
all three, not just the cell. Item 3 is a gameplay change.

**Verify.** The measurement itself is the verification for whether to proceed. After optimising, re-run
the Apparater's full click-target matrix documented in `docs/roles/apparater.md` — that role has an
extensive history of exactly this code being subtly wrong, and its doc records the cases that mattered.

**Depends on:** nothing, but treat the measurement step as mandatory. Do not optimise on the strength of
a worst-case bound alone.

---

## Suggested sequencing summary

| Order | Items | Gate |
|---|---|---|
| 0 | Land the uncommitted upgrade; capture warning baseline | — |
| 1 | 1.1 – 1.4 | build |
| 2 | 2.1 – 2.4 | build + look at the icons |
| 3 | 3 (localization) | build + key cross-check |
| 4 | 4.1, 4.2 (4.3 optional) | build + appearance/keybind playtest |
| 5 | 5.1/5.2 → 5.3; 5.4 independent | design sign-off + multi-client playtest |

Stages 1–3 are safe to land without a playtest gate beyond a smoke test. Stage 4 needs a targeted
playtest. Stage 5 should not be attempted without two clients and a designer decision on the win rules.
