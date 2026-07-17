using MiraAPI.Utilities.Assets;
using TownOfUs.Assets;
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

    // Generic HUD button placeholder, temporarily borrowing TOU-Mira's own Time Lord Rewind sprite
    // (same one the Astral's Phase button reuses) for ability buttons that don't have their own art
    // yet - a dedicated placeholder is coming later. Already sized like real button art, unlike the
    // much larger 288x288 role menu icons above.
    public static LoadableAsset<Sprite> ImpostorPlaceholderButton => TouCrewAssets.RewindSprite;
    public static LoadableAsset<Sprite> NeutralPlaceholderButton => TouCrewAssets.RewindSprite;
    public static LoadableAsset<Sprite> CrewmatePlaceholderButton => TouCrewAssets.RewindSprite;

    // In-world sprite for the RC-XD's car. No dedicated art yet - reuses the HUD button placeholder
    // so the car is at least visible in the world; swap for a real car sprite when it exists.
    public static LoadableAsset<Sprite> RcXdCarSprite => ImpostorPlaceholderButton;
}
