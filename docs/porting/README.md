# Porting Notes: AllTheRoles & TheOtherRoles → Super Squad Among Us

Research and reference docs from porting 10 custom roles (5 from AllTheRoles, 5 from TheOtherRoles).
**Implementation is complete** (all roles compiling, first-round playtesting done). See `docs/roles/<name>.md`
for per-role design decisions and current status; design choices override source-mod behavior.

**Attribution:** Role concepts and button art from AllTheRoles (https://github.com/Zeo666/AllTheRoles)
and TheOtherRoles (https://github.com/TheOtherRolesAU/TheOtherRoles). Credits in root `README.md`.

## Source mods & decompilation

- **TheOtherRoles (TOR):** readable C# source in `reference/TheOtherRoles/` (used directly for Ninja, Witch, Godfather, Mafioso, Eraser, Vulture).
- **AllTheRoles (ATR) v0.14.1:** decompiled via `ilspycmd` into `reference/AllTheRoles-decompiled/` (git-ignored local folder).
  To re-create: download `https://github.com/Zeo666/AllTheRoles/releases/download/0.14.1/AllTheRoles-0.14.1-x86-steam-itch.zip` and run
  `ilspycmd -p -o reference/AllTheRoles-decompiled --nested-directories -r <zip>/BepInEx/plugins -r <zip>/BepInEx/core <zip>/BepInEx/plugins/AllTheRoles.dll`.

## Asset extraction (if porting more roles)

**Button sprites from ATR bundle:** `pip install UnityPy` in a venv, then:
```python
from UnityPy import load
bundle = load('path/to/atr-win.bundle')
for obj in bundle.objects:
    if obj.type.name == 'Sprite' and obj.m_Name in [...]:
        obj.image.save(f'{obj.m_Name}.png')
```
Sprites already extracted to `SuperSquadAmongUs/Resources/{ImpButtons,NeutButtons,RoleIcons}/`.

**Role icon sourcing:** ATR and TOR ship no dedicated role icons. Current icons are byte-for-byte
copies of source-mod button art (best available). Proper TOU-style icons need to be commissioned.

**Sounds:** TOR's `warlockCurse` and `witchSpell` are in `reference/TheOtherRoles/TheOtherRoles/Resources/SoundEffects/toraudio`
(extractable with UnityPy). Check `Resources/SoundEffects/SoundEffectSourcesAndLicenses.md` for attribution.

## Status

**Wave 1 (ATR roles):** Astral, Sniper, Ninja, Witch (Impostor), Pelican (Neutral) — all implemented,
compiling, first playtest + fix round complete (2026-07-16). See `docs/roles/{astral,sniper,ninja,witch,pelican}.md`
for playtest history and follow-ups.

**Wave 2 (TOR roles):** Godfather, Mafioso, Mafia Janitor, Eraser, Vulture — all implemented (2026-07-16),
compiling, NOT yet tested in-game. See `docs/roles/{godfather,mafioso,mafia-janitor,eraser,vulture}.md`.

