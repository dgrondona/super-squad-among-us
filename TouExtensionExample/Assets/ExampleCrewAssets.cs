using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace TouExtensionExample.Assets;

public static class ExampleCrewAssets
{
    // THIS FILE SHOULD ONLY HOLD BUTTONS AND ROLE BANNERS, EVERYTHING ELSE BELONGS IN ExampleAssets.cs
    private const string ShortPath = "TouExtensionExample.Resources.CrewButtons";
    public static LoadableAsset<Sprite> ApparaterMapSprite { get; } = new LoadableResourceAsset($"{ShortPath}.ApparaterMapButton.png");
}
