# TheOtherRoles Ninja and Witch

Research notes during porting from **TheOtherRoles** (readable C# source in `reference/TheOtherRoles/`).

**Implementation status:** both roles ported and compiling. Design decisions and playtest history captured in `docs/roles/{ninja,witch}.md`.

**Attribution:** role concepts and button art from TheOtherRoles.

**Design approach:** faithfully ported from TOR source. Ninja and Witch implementations follow TOR conventions closely; see docs/roles/ for per-role design decisions and deviations (if any).

Ninja: TOR's mark-assassinate-invisibility pattern (5s arm delay, traces, optional tracking arrow).
Witch: TOR's channel-hex-at-ejection pattern (cumulative cooldown, asymmetric vote-save rule).
