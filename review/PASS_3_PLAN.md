# Review Pass 3 — targeted logic read of the ability implementations

A third review pass, deliberately narrow. Not a re-review: it covers only the files that passes 1 and 2
never read end to end.

**Status: COMPLETE (2026-09-24).** All 18 files read end to end. Produced SSA-023 – SSA-028 in
[FINDINGS.md](FINDINGS.md); see that file's "Pass 3 (complete)" note for what was cleared and why, and
for the two hypotheses this plan raised that were ruled out.

---

## Why this pass exists

Findings from passes 1–2 split cleanly by the method that produced them:

| Method | What it caught |
|---|---|
| Structured scans (grep for a known defect shape) | SSA-002 unwired icons, SSA-007 hardcoded strings, SSA-011 duplicate art, SSA-012 unused declarations, SSA-020 state-on-role-class, SSA-008/018 analyzer warnings |
| Reading a file end to end | SSA-019 solver `visited` bug, SSA-003 host-gated predicate, SSA-001/005 win-condition divergence, SSA-004 missing digest, SSA-009 per-tick scene scan |
| Cross-checking against `docs/roles/*.md` | SSA-022 false doc invariants, plus the three corrections to SSA-006/010/013 |

**Every logic defect came from a full read.** A scan can only find defect shapes already known, so the
~2,000 lines never read end to end are unexamined for this entire class — and they are the files with
real state machines, which is where the solver bug lived.

This pass closes that gap and nothing else.

## Scope

**In scope** — 18 files, ~2,000 lines, never read end to end:

| Tier | Files | Lines |
|---|---|---|
| 1 | `Buttons/Impostor/RcXdDeployButton.cs` | 362 |
| 2 | `Buttons/Impostor/NinjaMarkButton.cs`, `Buttons/Crewmate/DaddyHagridHideButton.cs`, `Buttons/Impostor/DumperCarryButton.cs` + `Modifiers/DumperCarryModifier.cs`, `Buttons/Impostor/DetonatorAttachButton.cs` | 824 |
| 3 | `Buttons/Impostor/WitchHexButton.cs`, `Buttons/Crewmate/SuiRetaliateButton.cs`, `Buttons/Crewmate/SuiProtectButton.cs`, `Buttons/Impostor/EraserEraseButton.cs` | 453 |
| 4 | `Modifiers/InvisibleBoyModifier.cs` (from L63), `Modifiers/GrantedSwoopModifier.cs`, `Buttons/Modifiers/SlideTackleButton.cs`, `Buttons/Modifiers/InvisibilityCloakButton.cs` | ~400 |
| 5 | `Patches/InvisibleBoyAdminPatch.cs`, `Patches/WitchMeetingPatch.cs`, `Patches/SniperAimPatch.cs`, `Patches/ApparaterMapClickPatch.cs` | 216 |

**Out of scope:**

- Anything already read end to end (all of `Modules/`, the five Neutral roles, the button/modifier base
  classes, `Assets/`, `Events/`, the seven patches already covered). Re-reading these is low yield —
  pass 2 produced corrections there, not new bugs.
- The 22 per-role `Options/` groups. Formulaic attribute declarations; a consistency scan already
  covered them and there is no logic to get wrong. One exception: confirm each group's option **ranges**
  are sane where a role's doc states intended values.
- Runtime behaviour. Still not executable here; this pass reads source.

## What to look for

Not a generic checklist — each item is a defect class with a **precedent in this codebase**, drawn from
`docs/il2cpp-gotchas.md` (which is this project's own catalogue of bugs it already shipped) and from the
three architectural rules in `docs/roles/gooper.md`.

**A. Scope conflation in bookkeeping.** The SSA-019 shape: a `visited`/`seen`/`handled` set recording a
permanent verdict for something that was only true of one path, pair, or frame. Look at every set or
flag that guards re-entry.

**B. Button-singleton state that never resets.** `il2cpp-gotchas.md` §"Button singletons persist for the
whole game process — stale ability state must self-heal". The grant architecture *mandates* per-ability
state on the button singleton (rule 1), so every such field needs a reset path across rounds. Candidates:
`SuiRetaliateButton`'s armed one-shot, `NinjaMarkButton`'s mark, `DetonatorAttachButton`'s bomb,
`ApparaterMapButton.teleported`/`openedFrame`, `SniperSnipeButton.aimLockActive`/`armedFrame`.

**C. `ClickHandler` overrides that drop the base gating.** §"Overriding `TownOfUsButton.ClickHandler`
drops the hacked/disabled gating". Known overriders: `DaddyHagridHideButton` (early-release branch),
`GrantedSwoopButton` (toggle). Verify each still blocks while hacked/disabled and still arbitrates its
keybind.

**D. Two-phase targeted buttons.** §"A two-phase *targeted* button's second phase is blocked by
`CanClick()`, not just `CanUse()`". `NinjaMarkButton` (mark → assassinate) is the archetype; check
`DumperCarryButton` (pick up → drop) and `DetonatorAttachButton` (attach → detonate) for the same shape.

**E. Buttons that stop ticking on death.** §"Role-gated buttons stop ticking the moment the player
dies" — MiraAPI only drives `FixedUpdate` while `Enabled(role)` is true, and death swaps the role.
`SniperSnipeButton` and `ApparaterMapButton` both override `Enabled` to stay alive while their effect is
active. Check every button holding cleanup-requiring state does the same: RC-XD (car on the map),
Dumper (carried body), Hagrid (hidden player), Detonator (live bomb).

**F. Host-gated vs all-client execution.** The SSA-003 shape. Anything mutating shared state inside
something only the host evaluates, or a local mutation assumed to be global. §"MiraAPI vanilla events
fire on every client — sync via local state changes, not host gating".

**G. Divergence from the ported source.** The SSA-001 shape: a formula copied from TOR/ATR/TOU-Mira and
then drifting. For each ported ability, diff against the original at the pinned tag and treat any
difference as intentional only if a doc says so.

**H. The three architectural rules** (`gooper.md`), per file: state on the button singleton not the role
class; RPC validators use `SenderIsOrHolds`; event handlers key on the modifier's caster/carrier, not
the caster's role type. Rules 2 and 3 verified clean in pass 2; rule 1 has two violations already.

**I. `Role.` dereference in a borrowable button** — null for a kit borrower (SSA-021's shape).

**J. Per-frame cost.** `FindObjectsOfType`, LINQ allocation, or physics queries in `FixedUpdate`/
`Update`. Two instances found already (SSA-009, SSA-017).

**K. Patch hygiene.** §"One throwing Harmony postfix skips the rest of the chain" — patches on shared
methods (`HudManager.Update`, `PlayerControl.FixedUpdate`, `MeetingHud.Update`) should run at
`Priority.First` and wrap their body in try/catch. `SniperAimPatch` and `InvisibleBoyVisibilityPatch`
already do; check `MafiaLabelsPatch`, `MafiosoGatePatches`, `WitchMeetingPatch`, `InvisibleBoyAdminPatch`.

**L. Doc/code divergence.** The SSA-022 shape: read each file's role doc and treat every *checkable*
factual claim as a claim to verify, not context.

## Per-file hypotheses

Stated up front so the pass is falsifiable — each is something specific to confirm or rule out, not a
vague "look for bugs". Being wrong about these is a fine outcome; it makes the file cheap to clear.

| File | Hypothesis to test |
|---|---|
| `RcXdDeployButton` | The deploy→drive→detonate→despawn lifecycle has races the existing `EnsureDestroyedLocally` safety nets paper over. Check: car ownership when the deployer dies mid-drive vs. `RcXdEvents`' disconnect path; whether `Enabled` keeps ticking for cleanup (E); whether `SelfDetonationFrame` can be stale into a later round (B) |
| `NinjaMarkButton` | Two-phase gating (D); mark survives a meeting or a role change; mark state on the singleton never cleared between rounds (B) |
| `DaddyHagridHideButton` | Its `ClickHandler` override drops hacked/disabled gating (C); early release vs. `CloakHiddenModifier.OnMeetingStart` double-removal |
| `DumperCarryButton` + `DumperCarryModifier` | Body renderers/collider restored on every exit path — meeting, death, disconnect, manual drop (E, and `DumperEvents` only covers disconnect) |
| `DetonatorAttachButton` | Whether one Detonator can hold multiple live bombs, and whether the bomb's owner check survives the kit being borrowed (H, I) |
| `WitchHexButton` | Hex ownership and `ResetCooldownAddition` across meetings; the commented-out block at L111 (SSA-018) may mark an abandoned path |
| `SuiRetaliateButton` / `SuiProtectButton` | `protectedTarget` is `gooper.md`'s cited worked example of rule 1 — verify it actually resets (B); one-shot retaliation armed state across rounds |
| `EraserEraseButton` | Immediate-erase RPC path vs. meeting resolution; commented-out block at `EraserEvents.cs:78` |
| `InvisibleBoyModifier` (rest) | Mushroom-mixup and comms-camouflage interaction; appearance self-heal parity with `TimedInvisibilityModifier` |
| `GrantedSwoopModifier` | `AutoStart => false` is a documented past bug — confirm it is still correct and commented |
| `SlideTackleButton` / `InvisibilityCloakButton` | Universal-modifier buttons: keybind arbitration (they share `ModifierAction` with granted Swoop) and `Enabled` gating |
| `InvisibleBoyAdminPatch` | Whether the admin-table masking leaks an invisible player's presence through a count, or over-masks and hides a visible one |
| `WitchMeetingPatch` | Per-frame overlay creation in `MeetingHud.Update` (J); patch hygiene (K) |
| `SniperAimPatch` / `ApparaterMapClickPatch` | Already partially seen; confirm both guard `Priority.First` + try/catch (K) |

## Method

Same standard as `FINDINGS.md`:

1. Read the file end to end. No grep-first.
2. Read its role doc alongside, checking factual claims (L).
3. For a ported ability, diff against the original in `reference/` at the pinned tag (G).
4. When `reference/` and the pinned package might disagree, decompile the real DLL — `ilspycmd -t <Type>
   ~/.nuget/packages/townofusmira/1.7.3/lib/net6.0/TownOfUsMira.dll` (§"reference/TOU-Mira can be ahead
   of the pinned package").
5. Every finding: file, line, evidence, failure scenario, impact, suggested fix, and an explicit
   **verified / unverified** marker with what would confirm it.
6. Anything that turns out to be deliberate goes in the "Deliberate patterns I checked and am *not*
   reporting" section, so a future pass doesn't re-file it.

New findings continue the existing numbering from **SSA-023**. Corrections to existing findings edit
them in place with a "CORRECTION" note, as SSA-010 and SSA-006 already show.

## Interaction with Phase 2

**This pass does not gate implementation.** Everything the high-value Phase 2 work touches was already
read end to end:

| Phase 2 item | Touches | Reviewed? |
|---|---|---|
| 5.5a/5.5b Apparater | `WalkableRegionSolver`, `ApparaterMapButton` | yes |
| 5.1/5.2 win conditions | four Neutral roles | yes |
| 5.3 digest rework | `PelicanEvents`, `KirbyEvents`, Pelican/Kirby roles | yes |
| 2.5 Vulture exclusion | `AbilityGrants` | yes |
| 2.1–2.4, 1.2, 1.3 | `MafiaLabelsPatch`, `Explode`, role `Configuration`s, `MafiosoGatePatches`, `SuperSquadColors`, `SentinelRole` | yes |

Two Phase 2 items reach into unread files, both low-risk: **1.4** deletes a commented-out block in
`WitchHexButton`, and **Stage 3** localization touches modifiers mechanically with behaviour-preserving
fallbacks.

**Recommended ordering:** start Phase 2 with 5.5a; run this pass alongside it or before Stage 3/4. If
the pass finds something in a file Stage 3 or 4 will touch, fold the fix into that item rather than
landing two changes to the same file.

⚠️ **One sequencing risk:** if this pass runs *after* Stage 3's localization sweep, every modifier file
will have changed, and a reviewer reading them fresh has to separate "this string moved" from "this
logic is wrong". Prefer running the pass **before** Stage 3.

## Exit criteria

- All 18 files read end to end, each either producing findings or explicitly recorded as clear.
- Every hypothesis above confirmed or ruled out, with evidence either way.
- `FINDINGS.md` updated (new SSA-023+ entries, in-place corrections, additions to the deliberate-patterns
  list); `CLEANUP_PLAN.md` updated with any new work items, sequenced against existing stages.
- Committed to `review/` only, in line with Phase 1's constraint.

**Estimated cost:** one session. ~2,000 lines of reading plus the doc cross-checks and upstream diffs.

**Expected yield — stated honestly:** a handful of findings, mostly Low/Medium. The code is careful and
the docs are unusually good, so I do not expect another SSA-019. The exception is RC-XD: it is the
largest file in the addon, has the longest follow-up list of any role doc, and coordinates a car, an
RPC-synced lifecycle, a self-kill and a keybind shared with the ghost Haunt menu. If a significant
defect remains anywhere, that is where I would bet on it.
