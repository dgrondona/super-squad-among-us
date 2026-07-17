using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace SuperSquadAmongUs.Assets;

public static class SuperSquadAssets
{
    private const string ShortPath = "SuperSquadAmongUs.Resources";
    public static LoadableAsset<Sprite> Banner { get; } = new LoadableResourceAsset($"{ShortPath}.SuperSquadBanner.png");

    // Generic team icons from Town of Us: Mira (reference/TOU-Mira/Images/Icons/), used as placeholders
    // for role menu icons that don't have their own art yet - see docs/roles/<name>.md "Known follow-ups".
    public static LoadableAsset<Sprite> ImpostorPlaceholderIcon { get; } = new LoadableResourceAsset($"{ShortPath}.Placeholders.Impostor.png", 200);
    public static LoadableAsset<Sprite> NeutralPlaceholderIcon { get; } = new LoadableResourceAsset($"{ShortPath}.Placeholders.Neutral.png", 200);
    public static LoadableAsset<Sprite> CrewmatePlaceholderIcon { get; } = new LoadableResourceAsset($"{ShortPath}.Placeholders.Crewmate.png", 200);

    // Generic HUD button placeholder (the vanilla Shapeshifter's Shift button art, from TheOtherRoles -
    // reference/TheOtherRoles/TheOtherRoles/Resources/ShiftButton.png), used for ability buttons that
    // don't have their own art yet. Sized like real button art (150x150 @ default PPU, unlike the much
    // larger 288x288 role menu icons above), so it no longer renders oversized in the HUD.
    public static LoadableAsset<Sprite> ImpostorPlaceholderButton { get; } = new LoadableResourceAsset($"{ShortPath}.Placeholders.GenericButton.png");
    public static LoadableAsset<Sprite> NeutralPlaceholderButton { get; } = new LoadableResourceAsset($"{ShortPath}.Placeholders.GenericButton.png");
    public static LoadableAsset<Sprite> CrewmatePlaceholderButton { get; } = new LoadableResourceAsset($"{ShortPath}.Placeholders.GenericButton.png");
}
