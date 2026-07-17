# Invisibility Cloak (universal modifier)

Universal game modifier (TOU-Mira `UniversalGameModifier`): the holder gets a bottom-left **Cloak**
button that turns them invisible for the configured duration (default 6s).

Files: `Modifiers/InvisibilityCloakModifier.cs` (the assigned game modifier),
`Modifiers/CloakInvisibilityModifier.cs` (the timed invisibility effect),
`Buttons/Modifiers/InvisibilityCloakButton.cs`, `Options/Modifiers/InvisibilityCloakOptions.cs`,
plus the shared `Options/Modifiers/SuperSquadModifierOptions.cs` (amount/chance).

## How it works

- **Assignment** is automatic: MiraAPI's `ModifierManager.AssignModifiers` scans every registered
  `GameModifier` with amount > 0 and chance > 0, across all Mira plugins — no registration code.
  Amount/chance live in the shared `SuperSquadModifierOptions` group (`ShowInModifiersMenu`, shown in
  TOU-Mira's Modifiers tab, same shape as TOU's `UniversalModifierOptions`); amount defaults to 0 so
  the modifier ships "off". Only assigned under TOU-Mira's non-vanilla role assignment.
- **One button modifier per player**: implements TOU's `IButtonModifier` marker and mirrors Button
  Barry's `IsModifierValidOn` exclusion, so the BottomLeft modifier-button slot never collides
  (the `UniversalGameModifier` base already enforces at most one universal modifier anyway).
- **The effect** is `CloakInvisibilityModifier : TimedInvisibilityModifier` — the same rendering as
  Ninja/Astral invisibility (blank appearance, `Player.Visible` re-asserted per tick for
  cams/admin, meeting cancels). One difference: `LocalViewerSeesOutline` (made `protected virtual`
  for this) drops the impostor clause — the cloak can be worn by anyone, so impostors see nothing;
  only the wearer and the informed dead get the faint outline.
- **Button** follows the BarryButton shape: `Enabled` checks `HasModifier<InvisibilityCloakModifier>`,
  `Keybinds.ModifierAction`, effect fill = invisibility duration, then cooldown. `OnEffectEnd` has a
  drift guard that removes a still-lingering effect modifier.

## Known follow-ups

- No dedicated art — button/wiki temporarily reuse TOU-Mira's Time Lord Rewind sprite
  (`TouCrewAssets.RewindSprite`, same one the Astral's Phase button borrows) until a dedicated
  placeholder exists.
- Needs in-game verification of the options landing in the Modifiers tab under pinned MiraAPI 0.3.5,
  and a second client confirming an impostor viewer sees no outline.
