# TheOtherRoles Mafia Trio

Research notes during porting from **TheOtherRoles** (readable C# source in `reference/TheOtherRoles/`).

**Implementation status:** Godfather, Mafioso, Mafia Janitor ported and compiling. Implementation details captured in `docs/roles/{godfather,mafioso,mafia-janitor}.md`.

**Attribution:** role concepts from TheOtherRoles.

**Trio mechanics:**
- **Godfather (Impostor):** vanilla impostor with team labels. Enables Mafioso's gating.
- **Mafioso (Impostor):** kill and sabotage buttons gated while Godfather lives (local UI only). Unlocked instantly when Godfather dies, no cooldown reset.
- **Mafia Janitor (Impostor):** separate from TOU-Mira's standalone Janitor. Participates in trio spawn.

**Spawn rule:** single spawn-chance roll; requires 3+ impostors and 3 open impostor role slots. Converts three vanilla impostors to the mafia roles (TOR-faithful).

**User decision (2026-07-16):** Mafia trio includes the Janitor, all spawn together via single roll (TOR style).
