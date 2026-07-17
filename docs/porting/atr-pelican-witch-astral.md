# AllTheRoles Pelican, Witch, and Astral

Research notes during porting from **AllTheRoles v0.14.1** (decompiled, obfuscated — mangled class/file names referenced in `reference/AllTheRoles-decompiled/`).

**Implementation status:** all three roles ported and compiling. Design decisions and playtest history captured in `docs/roles/{pelican,witch,astral}.md`.

**Attribution:** role concepts and button art from AllTheRoles.

**User-authoritative deviations from ATR:**
- **Pelican:** devoured players stay alive until the meeting (not killed immediately with death hidden). User's explicit choice — see design decisions in `docs/roles/pelican.md`. This is the only major deviation.
- **Witch:** follows ATR design closely (ATR Witch == TOR Witch); see docs/roles/witch.md.
- **Astral:** user-specified two-phase mechanic (ghost form + linger grace period); see docs/roles/astral.md for implementation.
