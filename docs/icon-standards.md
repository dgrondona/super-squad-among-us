# Icon & sprite standards

Size/resolution conventions for this repo's art, so new roles converge on one standard instead of
each picking their own. There are three unrelated systems here — role icons, HUD ability button
icons, and world-space sprites — each with its own size and `pixelsPerUnit` (PPU) convention.

**A note on "TOU-Mira as the standard":** TOU-Mira's actual in-game HUD icon assets are baked into
Unity-built asset bundles, not raw files — they aren't in the `reference/TOU-Mira` source checkout.
The only TOU-Mira art that IS present as raw PNGs (`reference/TOU-Mira/Images/Icons/`) is
promotional/wiki art for their README and external docs site, not the real game assets — its sizes
are inconsistent (a mix of 288×288 and 144×144 across 151 files) and it has no `.meta` import
settings, confirming it isn't Unity-imported game art. Where this doc says "matches TOU-Mira," it
means one of two things instead: (a) our own already-shipped, already-working assets (the strongest
ground truth we have, since they render correctly in-game today), or (b) MiraAPI's own official
example addon (`reference/MiraAPI/MiraAPI.Example/Resources/`), which is the closest thing to an
authoritative "here's the target size" reference for the framework both TOU-Mira and this addon are
built on.

## Role icons

**288×288 px, RGBA PNG, `pixelsPerUnit = 200`.** Used for the role list/settings card, wiki-style
display, etc. Folder: `Resources/RoleIcons/`. Referenced via a `LoadableResourceAsset(path, 200)` in
`Assets/SuperSquadRoleIcons.cs`, e.g.:

```csharp
public static LoadableAsset<Sprite> Sentinel { get; } = new LoadableResourceAsset($"{ShortPath}.RoleIcons.Sentinel.png", 200);
```

288×288 matches every currently-shipped role icon in this repo, and matches TOU-Mira's own generic
team placeholder icons (`Resources/Placeholders/{Crewmate,Impostor,Neutral}.png` are pixel-identical
copies of `reference/TOU-Mira/Images/Icons/{Crewmate,Impostor,Neutral}.png` — the one part of that
promotional folder that's confirmed to double as a real in-game asset, since MiraAPI's fallback
role-icon system uses it directly).

## Ability button icons (HUD)

**Target 128×128 px, RGBA PNG, transparent background, no explicit PPU** (MiraAPI's
`LoadableResourceAsset` defaults `pixelsPerUnit` to 100 when omitted — see
`reference/MiraAPI/MiraAPI/Utilities/Assets/LoadableResourceAsset.cs`). Folder: a per-team
`Resources/<Team>Buttons/` (`ImpButtons/`, `CrewButtons/`, `NeutButtons/`). Referenced via a bare
`LoadableResourceAsset(path)` (no PPU argument) in the matching `Assets/SuperSquad<Team>Assets.cs`.

128×128 is MiraAPI's own convention, not a number this repo invented: `MiraAPI.Example`'s sample
buttons (`ExampleButton.png`, `TeleportButton.png`) are both exactly 128×128. Since the HUD button
frame is a fixed size in world units and PPU=100 is fixed by the framework default, the *pixel
dimensions you choose directly set the icon's rendered size relative to vanilla ability icons* — a
150px icon renders visibly larger inside the same button frame than a 110px one. This repo's
currently-shipped buttons are NOT consistent (110–150px, oldest Sentinel-era assets are smallest);
that's a known, low-priority visual-consistency gap, not a bug (everything still renders/scales
fine) — new roles should target 128×128 so the set converges over time rather than drifting further.

## World-space sprites

Things placed as real `GameObject`s in the game world (not HUD elements) — e.g. RC-XD's car, the
Ninja's dash trace — have **no single standard size or PPU**. Each needs its PPU tuned so the object
reads at the correct in-game scale relative to players/the map, and that value is asset-specific:

- `RcXdCarSprite`: 450 PPU (see `docs/roles/rc-xd.md`) — tune this one further if the car reads
  too big/small in a live playtest, per that doc's open follow-up.
- `NinjaTraceSprite`: 225 PPU — TOR's exact original value (`Helpers.loadSpriteFromResources(...,
  225f)`); omitting the PPU argument here would silently default to MiraAPI's 100 and render the
  trace 2.25x too large (see `docs/roles/ninja.md`).

When porting a TOR/TOU-Mira visual, prefer their original PPU value over guessing; otherwise tune
by eye in a live game and record the chosen value (and why) in that role's doc.

## Placeholders — don't generate placeholder art

If a role/button has no final art yet, don't create AI-generated or stand-in art for it. Point the
role/button's `Assets/` property at `SuperSquadAssets.{Impostor,Neutral,Crewmate}Placeholder{Icon,Button}`
instead (see `docs/architecture.md`'s per-role file layout section) — these wrap TOU-Mira's own
generic team icons, already sized correctly for their respective slot (288×288 @ 200 PPU for the
icon variant, matching the "Role icons" section above; the button variant follows the ability-button
convention).

## Format notes

RGBA PNG with a transparent background is the default and preferred format. A couple of older
assets deviate (`SpellButtonMeeting.png` is indexed-palette, `NinjaTraceW.png` is grayscale+alpha) —
both render fine, but there's no reason to match them; use RGBA for anything new.
