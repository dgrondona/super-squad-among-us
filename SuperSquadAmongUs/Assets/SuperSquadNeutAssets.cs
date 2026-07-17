using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace SuperSquadAmongUs.Assets;

public static class SuperSquadNeutAssets
{
    // THIS FILE SHOULD ONLY HOLD BUTTONS AND ROLE BANNERS, EVERYTHING ELSE BELONGS IN SuperSquadAssets.cs
    private const string ShortPath = "SuperSquadAmongUs.Resources.NeutButtons";
    public static LoadableAsset<Sprite> SentinelVentSprite { get; } = new LoadableResourceAsset($"{ShortPath}.SentinelVentButton.png");
    public static LoadableAsset<Sprite> SentinelExplodeSprite { get; } = new LoadableResourceAsset($"{ShortPath}.SentinelExplodeButton.png");
    public static LoadableAsset<Sprite> SentinelKillSprite { get; } = new LoadableResourceAsset($"{ShortPath}.SentinelKillButton.png");
    // No dedicated button art yet - uses the generic Neutral placeholder (see SuperSquadAssets)
    // instead of the old AI-generated art.
    public static LoadableAsset<Sprite> PelicanDevourSprite => SuperSquadAssets.NeutralPlaceholderButton;
    public static LoadableAsset<Sprite> VultureEatSprite { get; } = new LoadableResourceAsset($"{ShortPath}.VultureButton.png");
    public static LoadableAsset<Sprite> VultureArrowSprite { get; } = new LoadableResourceAsset($"{ShortPath}.VultureArrow.png", 200);
}