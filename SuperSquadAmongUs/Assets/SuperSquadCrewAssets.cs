using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace SuperSquadAmongUs.Assets;

public static class SuperSquadCrewAssets
{
    // THIS FILE SHOULD ONLY HOLD BUTTONS AND ROLE BANNERS, EVERYTHING ELSE BELONGS IN SuperSquadAssets.cs
    private const string ShortPath = "SuperSquadAmongUs.Resources.CrewButtons";
    public static LoadableAsset<Sprite> ApparaterMapSprite { get; } = new LoadableResourceAsset($"{ShortPath}.ApparaterMapButton.png");
}
