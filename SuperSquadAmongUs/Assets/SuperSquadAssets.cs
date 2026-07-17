using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace SuperSquadAmongUs.Assets;

public static class SuperSquadAssets
{
    private const string ShortPath = "SuperSquadAmongUs.Resources";
    public static LoadableAsset<Sprite> Banner { get; } = new LoadableResourceAsset($"{ShortPath}.SuperSquadBanner.png");

    // Generic team icons from Town of Us: Mira (reference/TOU-Mira/Images/Icons/), used as placeholders
    // for roles/buttons that don't have their own art yet - see docs/roles/<name>.md "Known follow-ups".
    // Separate instances per usage since role icons and HUD buttons use different pixelsPerUnit conventions.
    public static LoadableAsset<Sprite> ImpostorPlaceholderIcon { get; } = new LoadableResourceAsset($"{ShortPath}.Placeholders.Impostor.png", 200);
    public static LoadableAsset<Sprite> NeutralPlaceholderIcon { get; } = new LoadableResourceAsset($"{ShortPath}.Placeholders.Neutral.png", 200);
    public static LoadableAsset<Sprite> ImpostorPlaceholderButton { get; } = new LoadableResourceAsset($"{ShortPath}.Placeholders.Impostor.png");
    public static LoadableAsset<Sprite> NeutralPlaceholderButton { get; } = new LoadableResourceAsset($"{ShortPath}.Placeholders.Neutral.png");
}
