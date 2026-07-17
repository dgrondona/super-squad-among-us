# Slide Tackle (universal modifier)

Universal game modifier (TOU-Mira `UniversalGameModifier`): the holder gets a bottom-left **Tackle**
button that lunges them to the nearest player and stuns the victim for the configured duration
(default 2s).

Files: `Modifiers/SlideTackleModifier.cs` (the assigned game modifier), `Modifiers/TackledModifier.cs`
(the stun), `Buttons/Modifiers/SlideTackleButton.cs`, `Options/Modifiers/SlideTackleOptions.cs`, plus
the shared `Options/Modifiers/SuperSquadModifierOptions.cs` (amount/chance).

## How it works

- **Assignment/exclusivity**: identical scaffolding to the Invisibility Cloak — see
  `docs/modifiers/invisibility-cloak.md` for the amount/chance plumbing and the `IButtonModifier`
  one-button-modifier rule.
- **The stun** is `TackledModifier : DisabledModifier` (house incapacitation pattern, see
  `docs/il2cpp-gotchas.md`): `CanUseAbilities`/`CanReport` false — but `CanBeInteractedWith` stays
  **true**, so a downed player can still be killed. Movement freeze is owner-side
  (`moveable = false` + `MyPhysics.ResetMoveState()`, self-healed per tick), with **no** NetTransform
  pause — unlike the carried state there's no pin fighting broadcasts, and the victim keeps
  broadcasting their (stationary) position. `AutoStart => true` + options duration; timer runs on
  every client; meeting clears it.
- **The lunge** is `RpcSnapTo(Target.GetTruePosition())` on the tackler — a placeholder until the
  slide animation (and the victim's falling-on-face animation) exist; the snap can then be replaced
  by the animation's movement.
- **Button** is a `TownOfUsTargetButton<PlayerControl>` (targeted, but not a role button), so it had
  to replicate `TownOfUsRoleButton`'s `IsTargetValid`/`SetOutline` (no venting targets, no
  untargetable players, no spectators) — that base doesn't carry them.
- **Interception applies**: as a `CustomActionButton<PlayerControl>`, tackling an alerted Veteran
  retaliates and tackling a shielded Elusive teleports the tackler. Emergent, correct, intended.

## Known follow-ups

- Slide/fall animations are the whole point later; the snap is a stand-in.
- An already-open task minigame isn't force-closed by the stun (accepted for a 2s effect).
- Needs 2-client verification: stun duration/recovery, momentum kill, no report/kill/ability while
  stunned, stunned player still killable, vent-entry blocked.
- No dedicated art — button/wiki use the generic HUD button placeholder (`Resources/Placeholders/GenericButton.png`, the vanilla Shapeshifter's Shift button art from TheOtherRoles).
