using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace SuperSquadAmongUs.Assets;

public static class SuperSquadImpAssets
{
    // THIS FILE SHOULD ONLY HOLD BUTTONS AND ROLE BANNERS, EVERYTHING ELSE BELONGS IN SuperSquadAssets.cs
    private const string ShortPath = "SuperSquadAmongUs.Resources.ImpButtons";
    public static LoadableAsset<Sprite> NinjaMarkSprite { get; } = new LoadableResourceAsset($"{ShortPath}.NinjaMarkButton.png");
    public static LoadableAsset<Sprite> NinjaAssassinateSprite { get; } = new LoadableResourceAsset($"{ShortPath}.NinjaAssassinateButton.png");
    public static LoadableAsset<Sprite> NinjaTraceSprite { get; } = new LoadableResourceAsset($"{ShortPath}.NinjaTraceW.png", 225);
    public static LoadableAsset<Sprite> WitchHexSprite { get; } = new LoadableResourceAsset($"{ShortPath}.HexButton.png");
    public static LoadableAsset<Sprite> WitchHexedOverlaySprite { get; } = new LoadableResourceAsset($"{ShortPath}.SpellButtonMeeting.png", 225);
    // No dedicated button art yet - uses the generic Impostor placeholder (see SuperSquadAssets)
    // instead of the old AI-generated art.
    public static LoadableAsset<Sprite> SniperSnipeSprite => SuperSquadAssets.ImpostorPlaceholderButton;
    // Source art is 1024x1024 (not the usual ~128px target - see docs/icon-standards.md) to stay
    // crisp. 800 PPU would match the rest of the HUD row exactly (1024/800 = 128/100); tuned down
    // from there by eye since the icon read too small at parity.
    public static LoadableAsset<Sprite> RcXdDeploySprite { get; } = new LoadableResourceAsset($"{ShortPath}.RcXdDeploy.png", 600f);
    public static LoadableAsset<Sprite> RcXdDetonateSprite => SuperSquadAssets.ImpostorPlaceholderButton;
    public static LoadableAsset<Sprite> RcXdCarSprite { get; } = new LoadableResourceAsset($"{ShortPath}.RcXdCar.png", 450);
    public static LoadableAsset<Sprite> SniperGuideSprite { get; } = new LoadableResourceAsset($"{ShortPath}.SniperGuide.png");
    public static LoadableAsset<Sprite> MafiaJanitorCleanSprite { get; } = new LoadableResourceAsset($"{ShortPath}.MafiaJanitorCleanButton.png");
    public static LoadableAsset<Sprite> EraserSprite { get; } = new LoadableResourceAsset($"{ShortPath}.EraserButton.png");
}
