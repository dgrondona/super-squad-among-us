using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace TouExtensionExample.Assets;

public static class ExampleRoleIcons
{
    // THIS FILE SHOULD ONLY HOLD ROLE ICONS

    private const string ShortPath = "TouExtensionExample.Resources";

    // Neutrals
    public static LoadableAsset<Sprite> Sentinel { get; } = new LoadableResourceAsset($"{ShortPath}.RoleIcons.Sentinel.png", 200);

    // Crewmates
    public static LoadableAsset<Sprite> Apparater { get; } = new LoadableResourceAsset($"{ShortPath}.RoleIcons.Apparater.png", 200);
}