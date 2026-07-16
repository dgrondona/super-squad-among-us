using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace SuperSquadAmongUs.Assets;

public static class SuperSquadImpAssets
{
    // THIS FILE SHOULD ONLY HOLD BUTTONS AND ROLE BANNERS, EVERYTHING ELSE BELONGS IN SuperSquadAssets.cs
    private const string ShortPath = "SuperSquadAmongUs.Resources.ImpButtons";
    public static LoadableAsset<Sprite> AstralFormSprite { get; } = new LoadableResourceAsset($"{ShortPath}.AstralButton.png");
    public static LoadableAsset<Sprite> NinjaMarkSprite { get; } = new LoadableResourceAsset($"{ShortPath}.NinjaMarkButton.png");
    public static LoadableAsset<Sprite> NinjaAssassinateSprite { get; } = new LoadableResourceAsset($"{ShortPath}.NinjaAssassinateButton.png");
    public static LoadableAsset<Sprite> NinjaTraceSprite { get; } = new LoadableResourceAsset($"{ShortPath}.NinjaTraceW.png", 225);
    public static LoadableAsset<Sprite> WitchHexSprite { get; } = new LoadableResourceAsset($"{ShortPath}.HexButton.png");
    public static LoadableAsset<Sprite> WitchHexedOverlaySprite { get; } = new LoadableResourceAsset($"{ShortPath}.SpellButtonMeeting.png", 225);
    public static LoadableAsset<Sprite> SniperSnipeSprite { get; } = new LoadableResourceAsset($"{ShortPath}.SniperButton.png");
    public static LoadableAsset<Sprite> SniperGuideSprite { get; } = new LoadableResourceAsset($"{ShortPath}.SniperGuide.png");
    public static LoadableAsset<Sprite> MafiaJanitorCleanSprite { get; } = new LoadableResourceAsset($"{ShortPath}.MafiaJanitorCleanButton.png");
    public static LoadableAsset<Sprite> EraserSprite { get; } = new LoadableResourceAsset($"{ShortPath}.EraserButton.png");
}
