# TOU-Mira Pattern Reference

Quick lookup: which TOU-Mira / MiraAPI role implements mechanic X.

## Mechanic → Template Mapping

| Mechanic | Template Role | Key Files |
|----------|--------------|-----------|
| Invisibility | Swooper (addon: InvisibleBoyModifier preferred) | `SwoopModifier.cs`, `SwooperSwoopButton.cs` |
| Directional/Ranged Attack | Hunter (hybrid: no exact match) | `HunterKillButton.cs`, `MiscUtils.PhysicsHelpers` |
| Target Marking + Arrow | ArrowTargetModifier (Sonar subclass) | `ArrowTargetModifier.cs`, `SonarArrowTargetModifier.cs` |
| Delayed Death at Meetings | VotingCompleteEvent + modifier | `Plaguebearer` (infection state) |
| Freeze/Pin Player in Place | Ambusher (SetPaused + ResetMoveState) | `AmbusherModifier.cs` (addon pattern) |
| Channel/Cast Time | EffectDuration button pattern | `SwooperSwoopButton.cs` (example) |
| Neutral Win Condition | Arsonist (TOU-Mira neutral killer) | `ArsonistRole.cs` |
| Impostor Template | Vanilla Impostor | `ImpostorRole.cs` |
| Meeting Overlay UI | Lovers / Traitor (custom RoleUI layer) | MiraAPI's `RoleUILayer` |
| Synced Modifiers | RpcAddModifier / RpcRemoveModifier | MiraAPI standard |

## Critical Gotchas

- **Player.Visible is local-only** — camera/admin honor it, sprite alpha alone doesn't hide.
- **Vanilla vent/ladder flip Visible back on** — re-assert every FixedUpdate.
- **Collider self-heal** — vanilla kill animations re-enable collider; re-disable if needed in FixedUpdate.
- **Arrow cleanup** — call `gameObject.DeepDestroy()` on deactivate, not just hiding.
- **Channel cancellation** — MiraAPI's `EffectDuration` auto-expires; manually cancel mid-channel if needed (Witch pattern).
- **Modifier lifetime** — `TimedModifier` expires client-side on each client independently; no RPC coordination needed.
- **Meeting visibility** — root layer may not be in meeting camera's cull mask; use a rendered child's layer instead (Witch hex overlay fix).

## Per-Role Template Checklist

- **Sniper** → Hunter (targeting) + custom aiming (no TOU template)
- **Ninja** → two-state button + InvisibleBoyModifier + ArrowTargetModifier
- **Witch** → EffectDuration channel + VotingCompleteEvent death resolution
- **Astral** → Swooper invisibility + collider disable/re-enable + teleport (RpcSnapTo)
- **Pelican** → Ambusher (pin/freeze) + Arsonist (neutral win) + InvisibleBoy (hide on cams)
