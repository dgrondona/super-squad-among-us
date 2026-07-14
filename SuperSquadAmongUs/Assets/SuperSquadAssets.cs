using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace SuperSquadAmongUs.Assets;

public static class SuperSquadAssets
{
    private const string ShortPath = "SuperSquadAmongUs.Resources";
    public static LoadableAsset<Sprite> Banner { get; } = new LoadableResourceAsset($"{ShortPath}.SuperSquadBanner.png");
}
