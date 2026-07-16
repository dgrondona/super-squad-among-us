# Porting AllTheRoles roles → Super Squad Among Us

Working doc for porting five roles from the **AllTheRoles (ATR)** mod
(https://github.com/Zeo666/AllTheRoles by Zeo666 and contributors) into this addon, re-implemented
on MiraAPI/TOU-Mira. Roles: **Astral, Sniper, Ninja, Witch** (Impostor) and **Pelican** (Neutral).

> **Credits requirement:** these five roles' designs and their button art come from AllTheRoles.
> A credits section must be added to the root `README.md` before release (role concepts + icons:
> AllTheRoles by Zeo666 et al.; ATR itself descends from TheOtherRoles, GPL-3.0).

## Source-of-truth situation (important)

**Design authority:** the user's own role descriptions (captured in each role's plan section below)
override whatever ATR/TOR does. ATR decompiled code and TOR source are *implementation hints* and
fill in unspecified details (option ranges, rendering tricks, edge cases), not the spec.

- `reference/TheOtherRoles/` — **readable C# source** of TheOtherRoles (TOR), the mod ATR descends
  from. TOR has Ninja and Witch (ATR's are near-copies — option lists match), so port those from TOR
  source rather than the obfuscated ATR decompile. Research: `tor-ninja-witch.md`.
- The ATR GitHub repo publishes **no source code** — `reference/AllTheRoles/` (their `main` branch)
  is cosmetics + release links only.
- We downloaded release **v0.14.1** and decompiled `AllTheRoles.dll` with `ilspycmd` into
  `reference/AllTheRoles-decompiled/` (git-ignored, local only). The DLL is **Dotfuscator-obfuscated**:
  file/class/member names are mangled (`a0.cs` … `kz.cs`), but **string literals survive**, and the
  enums under `AllTheRoles-decompiled/AllTheRoles/Modules/Data/` kept real names (`RoleEnum.cs` etc.).
- All user-facing strings: `reference/AllTheRoles-decompiled/AllTheRoles.Resources.Languages.Lang.dat`
  (plain `"key" : "value"` text). Grep it for `role.<name>.` / `option.impostor.<name>.` keys.
- To re-create the decompile from scratch: download
  `https://github.com/Zeo666/AllTheRoles/releases/download/0.14.1/AllTheRoles-0.14.1-x86-steam-itch.zip`,
  unzip, then
  `ilspycmd -p -o reference/AllTheRoles-decompiled --nested-directories -r <zip>/BepInEx/plugins -r <zip>/BepInEx/core <zip>/BepInEx/plugins/AllTheRoles.dll`.

## Assets — DONE (extracted)

Button sprites were extracted from the embedded Unity bundle
(`AllTheRoles.Resources.atr-win.bundle`) with UnityPy (`pip install UnityPy` in a venv; load bundle,
save `Sprite` objects whose `m_Name` matches) into the repo:

- `SuperSquadAmongUs/Resources/ImpButtons/`, from ATR's bundle: `AstralButton.png`, `HexButton.png`,
  `HexSymbol.png` (mark shown over hexed players), `NinjaKillButton.png`, `SniperButton.png`,
  `SniperGuide.png` (aim-direction arrow).
- `SuperSquadAmongUs/Resources/ImpButtons/`, copied from TOR's readable repo
  (`reference/TheOtherRoles/TheOtherRoles/Resources/`): `NinjaMarkButton.png`,
  `NinjaAssassinateButton.png` (proper two-state pair — ATR only had a placeholder for
  assassinate), `NinjaTraceW.png` (the trace sprite), `SpellButton.png`, `SpellButtonMeeting.png`
  (Witch's meeting overlay). For Witch, pick TOR's SpellButton or ATR's HexButton at
  implementation time; delete the loser.
- `SuperSquadAmongUs/Resources/NeutButtons/`: `DevourButton.png` (ATR sprite name `PelicanButton`,
  purple crewmate swallowing a player — this IS the button ATR uses, per `hk.cs` → `f9.n()`),
  `DevourButtonAlt.png` (ATR sprite name `DevourButton`, pink crewmate eating — unused by Pelican,
  delete or repurpose).

ATR has **no TOU-style role icons** for these roles (only button art), and the ATR bundle's only
AudioClip is `HeartbeatPing` (not ours). Role icons for `Resources/RoleIcons/` still need to be
sourced/made. Sounds: TOR plays `warlockCurse` (ninja mark) and `witchSpell` (hex cast) from the
Unity bundle `reference/TheOtherRoles/TheOtherRoles/Resources/SoundEffects/toraudio` — extractable
with the same UnityPy recipe if we want them; check
`Resources/SoundEffects/SoundEffectSourcesAndLicenses.md` for attribution requirements first.

## Research notes (written by research agents)

- `atr-sniper-ninja.md` — ATR behavior spec, options w/ defaults, RPCs, edge cases, mangled-file map.
- `atr-pelican-witch-astral.md` — same for the other three.
- `tor-ninja-witch.md` — TOR's readable Ninja/Witch implementation (preferred reference for those two).
- `tou-mira-patterns.md` — which TOU-Mira/MiraAPI roles to use as implementation templates per mechanic.

## Status / resume point

- [x] Branch `dev/role-port` created.
- [x] ATR v0.14.1 decompiled into `reference/AllTheRoles-decompiled/`.
- [x] Button sprites extracted into `Resources/` (see above).
- [x] Research notes complete for ATR + TOU-Mira patterns (ATR option defaults, RPC IDs, and the
      Pelican devour flow were spot-checked against the decompiled source and matched).
- [x] TOR Ninja/Witch research (`tor-ninja-witch.md`) done; option defaults spot-checked against
      `CustomOptionHolder.cs` and folded into sections 2–3 below. ATR Witch == TOR Witch (+ vent
      option). Ninja defaults differ between mods (using TOR's; deltas flagged in section 2).
- [x] TOR Ninja/Witch sprites copied into `Resources/ImpButtons/` (see Assets).
- [x] Porting plan updated with the user's authoritative role descriptions (2026-07-15): Pelican
      devour = alive spectators who die at meeting; Astral = 15s ghost + teleport-back + 5s
      invisibility grace; Sniper = 10s aim window, piercing cross-map line shot. Ninja/Witch follow
      TOR behavior.
- [x] Credits section added to root `README.md`.
- [x] **All five roles implemented and compiling** (commits `8996e37` Astral, `cd1598b` the rest).
- [x] Per-role docs `docs/roles/<name>.md` written.
- [x] Devour button sprite picked (`DevourButton.png`); unused alt deleted.
- [x] **First playtest (2026-07, solo-ish) + fix round done.** Findings and fixes — full details in
      each role's `docs/roles/<name>.md` "Playtest history" section:
  - Sniper "didn't work": clicks were polled from the button's FixedUpdate (fixed tick, drops
    clicks) → moved to a per-frame `HudManager.Update` patch (`SniperAimPatch`); hit test was
    line-vs-center-point → now line-vs-body-sprite-circle; also added first-death-shield /
    untargetable / hacked gating.
  - Astral: crew-view outline report — code analysis says crew clients render nothing (matches
    Swooper); fixed two real remote-visibility gaps anyway (linger handoff now local per client,
    appearance self-heals per tick). Needs re-test from a genuine crewmate client.
  - Witch: meeting overlay layer now taken from `voteArea.Megaphone` like TOR (root layer may not be
    in the meeting camera's cull mask); cumulative cooldown now persists across meetings (TOR only
    resets per game — earlier per-meeting reset was wrong).
  - Ninja: `ClickHandler` override was missing TOU's hacked/disabled gating — restored.
  - Pelican (proactive, still untested): freeze now uses Ambusher's owner-only
    `SetPaused`/`ResetMoveState` pattern; new `DevouredDisabledModifier` (TOU `DisabledModifier`)
    blocks report/abilities/targeting while devoured; Pelican disconnect now releases the stomach.
- [ ] **Re-test in a real lobby** — per-role checklists live in the "Playtest history" /
      "follow-ups" sections of `docs/roles/*.md`.
- [ ] Role icons: current icons are placeholder copies of the button art in
      `Resources/RoleIcons/` — replace with proper TOU-style icons.

## Post-implementation follow-ups

- **Re-test needed** (see per-role docs): Sniper end-to-end; Astral crew-view invisibility from a
  crewmate client; Witch meeting overlay; Pelican everything (needs 2+ players); Ninja
  teleport-kill + traces across clients.
- Sounds: no cast/mark/shot sounds yet. TOR's `warlockCurse`/`witchSpell` clips are in the TOR
  `toraudio` bundle (see Assets section); a shot sound needs sourcing.
- Pelican: devoured players keep their tasks (crew could in theory still win on tasks while
  devoured; consoles are blocked so they can't *complete* new ones) — decide if outstanding tasks
  should count.

## Porting plan

Implementation order is chosen so shared infrastructure gets built once and reused, starting with
the role that overlaps most with code this addon already has:

### 1. Astral (Impostor) — user spec; hints in `atr-pelican-witch-astral.md`

**User spec (authoritative):** press the button to phase into ghost form — invisible AND walks
through walls. Can kill while in ghost form, and has **15 seconds** to do so. When the 15s expire
they are **teleported back to the spot they phased from**, then stay **invisible for 5 more
seconds** (grace period to walk away from the phase spot) before becoming visible. During the grace
period collision is back to normal (no wall-walking) — only the invisibility lingers.
- Template: Swooper (TOU-Mira impostor w/ effect button) + this addon's `InvisibleBoyModifier`
  rendering approach (already handles cams/admin) + Apparater's teleport for the snap-back.
  ATR hint: collider-disable for wall-walking, `Color.clear` w/ 0.1 self-alpha for rendering.
- Files: `Roles/Impostor/AstralRole.cs`, `Buttons/Impostor/AstralFormButton.cs` (effect button,
  duration = form duration), `Modifiers/AstralFormModifier.cs` (two-phase: ghost phase → grace
  phase; transparency + collider off + snap-back state), `Options/Roles/Impostor/AstralOptions.cs`.
- Options: cooldown 25 (10–40, 2.5); form duration **15** (5–30, 1); post-return invisibility **5**
  (0–15, 1); can-vent false. (Defaults from user spec; ranges extrapolated from ATR's.)
- Watch: kill button must stay usable in form; collider re-enable on EVERY exit path (meeting,
  death, disconnect); decide meeting-mid-form behavior (ATR: exit without teleport — flag for user
  if we deviate); IL2CPP — don't call `base.X()` (see `docs/il2cpp-gotchas.md`).

### 2. Ninja (Impostor) — port from TOR source (`tor-ninja-witch.md`); ATR notes in `atr-sniper-ninja.md`

TOR flow (verified against `Buttons.cs`/`CustomOptionHolder.cs`): **mark** a nearby target
(normal kill-distance targeting) — the button then shows a 5s timer before it can be pressed again
as **assassinate**. Assassinate is an unchecked murder that snaps the ninja onto the target from
anywhere on the map; fading **traces** spawn at the ninja's pre-kill position and at the victim,
and the ninja goes **invisible** for the invis duration to slip away. If knows-location is on, an
arrow tracks the marked target (client-side only — not on cams/admin; the traces ARE world objects
everyone sees).
- Template: one two-state button (mark↔assassinate, sprite swaps); `ArrowTargetModifier` (TOU-Mira)
  for the tracking arrow; InvisibleBoy/Swooper pattern for invisibility (TOR does it as
  empty-cosmetics + alpha 0, 0.1 for impostors/dead — use our modifier approach instead, same
  visible result); MiraAPI custom murder for the kill so shields apply.
- Files: `Roles/Impostor/NinjaRole.cs`, `Buttons/Impostor/NinjaMarkButton.cs` (two-state button),
  `Modifiers/NinjaMarkModifier.cs` (on target, carries arrow), `Modules/NinjaTrace.cs` (fading
  trace sprite — TOR's `NinjaTraceW.png`, copied into `Resources/ImpButtons/`),
  `Options/Roles/Impostor/NinjaOptions.cs`.
- Options (TOR defaults; ATR deltas in parens — **using TOR's, flag if user disagrees**):
  mark cd 30 (10–120, 5) _(ATR: 25, 10–40)_; knows-location true; trace duration 5 (1–20, 0.5);
  trace color fade 2 (0–20, 0.5); invis duration **3** (0–20, 1) _(ATR: 10, 5–40)_; can-vent false
  (ATR-only option, kept).
- Watch: trace fade = color lerp ninja-color→green over fade time, then alpha-out over
  `min(1s, duration/2)` at the end; invisibility persists through meetings in TOR (timer keeps
  ticking) — decide if we clear it on meeting instead; mark survives until assassinate/meeting;
  trace placement order in TOR is start-trace → invisibility → murder → end-trace.

### 3. Witch (Impostor) — port from TOR source (`tor-ninja-witch.md`); ATR notes in `atr-pelican-witch-astral.md`

**Resolved:** ATR's Witch IS TOR's Witch — identical options and defaults (verified in both
sources); ATR only adds a Can Vent toggle. One design, one port.

Channeled spell/hex (cast duration; cancels if the closest-target changes mid-channel) marks
players. Marked players show an overlay sprite next to their name in meetings (visible to
everyone). At **meeting end** all marked players die exile-style (no bodies, no kill animation)
UNLESS the witch was **exiled** that meeting (option, default on). Each successful cast adds a
cumulative +10s to the next hex cooldown, and (option) also triggers the witch's kill cooldown.
- Template: effect-button channel (MiraAPI `EffectDuration`/`OnEffectEnd`) for the cast;
  meeting-end resolution hooks into the exile flow (TOR patches `ExileController.OnDestroy`; check
  MiraAPI's meeting/exile events — `VotingCompleteEvent` exists but fires before exile, TOU-Mira
  may expose a better post-exile hook); `Modifiers/HexedModifier.cs` on victims carries the mark +
  meeting overlay.
- Files: `Roles/Impostor/WitchRole.cs`, `Buttons/Impostor/HexButton.cs`,
  `Modifiers/HexedModifier.cs`, `Options/Roles/Impostor/WitchOptions.cs`.
- Options (identical in TOR and ATR, verified): hex cd 30 (10–120, 5); additional cd 10 (0–60, 5);
  can-hex-anyone false; cast duration 1 (0–10, 1); trigger-both true; vote-witch-saves true;
  can-vent true (ATR-only option, kept).
- Watch (TOR subtleties easy to get wrong): the save-on-vote is **asymmetric** — targets are saved
  only if the witch is *exiled* (or dies to lover-exile), NOT if the witch is murdered during the
  meeting; hexes persist across meetings until resolved; dead targets are skipped at resolution
  (kill re-validated per target); cumulative cooldown addition — verify TOR's reset point (likely
  `clearAndReload`/meeting) and mirror it; deaths are exile-deaths so no bodies and death-reason
  bookkeeping differs.

### 4. Pelican (Neutral Killing) — user spec; hints in `atr-pelican-witch-astral.md`

**User spec (authoritative — deliberately different from ATR):** devoured players **stay alive**
and effectively **spectate the Pelican** (hidden from everyone, no tasks/buttons, camera-follow /
position pinned to the Pelican). When a **meeting is called, all devoured players die**. If the
**Pelican is killed, everyone in its stomach is released** (alive, at the Pelican's position).
Because meetings empty the stomach by killing, the vote-out case is moot — release-on-death only
happens mid-round. Neutral solo win condition.
- ATR delta (do NOT copy): ATR kills the target instantly and hides the death; we keep them alive
  until meeting. ATR's useful hints: Ghost-layer + `Visible=false` for hiding, per-physics-tick
  position pinning to the Pelican, vitals-panel patches, disabling use/report buttons + chat.
- Template: Glitch/Sentinel for the neutral-killer win condition (`WinConditionMet()`);
  `Modifiers/DevouredModifier.cs` for the eaten state (hide + follow + suppress interactions);
  InvisibleBoy's cams/admin hiding; MiraAPI `StartMeetingEvent` (or ReportBody path) for the
  meeting-start deaths.
- Files: `Roles/Neutral/PelicanRole.cs`, `Buttons/Neutral/DevourButton.cs`,
  `Modifiers/DevouredModifier.cs`, `Options/Roles/Neutral/PelicanOptions.cs`.
- Options: devour cd 15 (10–60, 2.5), can-vent false.
- Watch: most invasive role of the five. Devoured players must not be reportable/targetable, must
  not count as dead for win conditions while devoured, and their meeting-start deaths need corpses
  suppressed (they die "in the stomach" — no bodies on the map).
- **User decision (2026-07-15):** Pelican is Neutral and wins like a neutral killer, following the
  same win-condition format as TOU-Mira's **Arsonist** — copy that role's win-condition shape.

### 5. Sniper (Impostor) — user spec; hints in `atr-sniper-ninja.md`

**User spec (authoritative — deliberately different from ATR):** press the Snipe button, then the
sniper has **10 seconds to click somewhere on screen**. A bullet travels **across the whole map in
a straight line** defined by the sniper's body position and the clicked point. **Everyone the
bullet hits dies — it pierces through multiple people and goes through walls.**
- ATR delta (do NOT copy): ATR kills only the *closest* target within a 20-unit cone and uses
  right-click while a guide sprite follows the mouse. We keep ATR's useful hints: the
  perpendicular-distance line test (0.2-unit half-width via Atan2 coordinate rotation, `ig.cs`
  lines ~460–470), `SniperGuide.png` for an aim indicator, "Shots Fired!" sniper-only notification.
- Template: none for aiming (fully custom — last for a reason). Kills via MiraAPI custom murder RPC
  per victim (standard murder path so shields still apply). RPC payload: the two line points (all
  clients replay the same line test) or the victim ID list resolved by the shooter — decide during
  implementation; victim-list is simpler and desync-proof.
- Files: `Roles/Impostor/SniperRole.cs`, `Buttons/Impostor/SnipeButton.cs` (arms aim mode w/ 10s
  window), `Modules/SniperShot.cs` (click capture, line hit test against all alive players, bullet
  visual), `Options/Roles/Impostor/SniperOptions.cs`.
- Options: snipe cd 25 (10–40, 2.5); aim window **10** (5–30, 1); can-kill-impostors false;
  can-vent false. (Bullet width: keep ~0.2 units, constant unless it plays badly.)
- Watch: click capture must not fire when clicking UI (buttons/minimap); clear aim state on
  meeting/death/vent.
- **User decision (2026-07-15):** the bullet is NOT visible to others by default, but it's a
  toggle option ("bullet visible" bool, default false) — a future similar role may default it on.

### Cross-cutting notes

- All four impostor roles keep the vanilla kill button (Sniper's snipe *replaces* normal kills —
  verify in `ig.cs` whether ATR Sniper also has a normal kill; the patterns doc's Hunter-button notes
  cover both cases). Vent access is per-role options via TOU-Mira's standard vent-option handling.
- New locale keys per role in `Resources/Locale/en_US.xml` (`SuperSquadRole...`/`SuperSquadOption...`).
- New team folder conventions introduced here: `Roles/Impostor/`, `Buttons/Impostor/`,
  `Options/Roles/Impostor/`, `Resources/ImpButtons/` — first impostor roles in this addon.
- Role colors → `SuperSquadColors.cs` (Pelican purple `#6A15AB` from ATR; impostor roles use
  `TownOfUsColors.Impostor` red).
- RPC IDs: use this addon's own MethodRpc range, NOT ATR's (114/99/89/92/93/113 are ATR-internal).
- Don't blind-copy ATR edge-case behavior that MiraAPI already handles (shield checks, kill
  validation, cooldown sync) — use the MiraAPI path and verify equivalence instead.
