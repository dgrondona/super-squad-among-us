# AllTheRoles Sniper & Ninja

Research notes during porting from **AllTheRoles v0.14.1** (decompiled, obfuscated — mangled class/file names referenced in `reference/AllTheRoles-decompiled/`).

**Implementation status:** both roles ported and compiling. Design decisions and playtest history captured in `docs/roles/{sniper,ninja}.md`.

**Attribution:** role concepts and button art from AllTheRoles.

**User-authoritative deviations from ATR:**
- **Sniper:** bullet renders invisibly (user decision 2026-07-16) — ATR renders a visible guide sprite during aiming and a bullet trajectory on fire; our version has neither. Aiming mechanic (click-to-fire, not right-click-to-aim) is user-specified.
- **Ninja:** follows ATR's design closely; see docs/roles/ninja.md for implementation details.
