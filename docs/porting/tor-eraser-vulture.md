# TheOtherRoles Eraser and Vulture

Research notes during porting from **TheOtherRoles** (readable C# source in `reference/TheOtherRoles/`).

**Implementation status:** Eraser and Vulture ported and compiling. Implementation details captured in `docs/roles/{eraser,vulture}.md`.

**Attribution:** role concepts from TheOtherRoles.

**Eraser (Impostor):**
- Target-and-mark ability: click a player to mark them as future-erased.
- Erase resolves at the next meeting's exile screen — target loses their modded role (becomes plain Crewmate).
- Cumulative cooldown: each use adds 10s to the next erase cooldown (persists all game, like Witch).
- Option: erase crew only or anyone.

**Vulture (Neutral):**
- Button ability: eat nearby corpses (single-target, like vanilla kill/sabotage range).
- Eaten bodies disappear for everyone (synced RPCs).
- Win condition: game ends instantly when Nth body eaten (TOR-style instant win, not end-of-game check).
- Options: arrows to bodies, can vent, impostor vision.

**User decisions (2026-07-16, all TOR-faithful):**
- Erased players NOT notified — they just notice their role UI is gone.
- Vulture win is instant (game ends as soon as threshold is met).
