using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace SuperSquadAmongUs.Assets;

public static class SuperSquadRoleIcons
{
    // THIS FILE SHOULD ONLY HOLD ROLE ICONS

    private const string ShortPath = "SuperSquadAmongUs.Resources";

    // Neutrals
    public static LoadableAsset<Sprite> Sentinel { get; } = new LoadableResourceAsset($"{ShortPath}.RoleIcons.Sentinel.png", 200);

    // Crewmates
    public static LoadableAsset<Sprite> Apparater { get; } = new LoadableResourceAsset($"{ShortPath}.RoleIcons.Apparater.png", 200);
    public static LoadableAsset<Sprite> InvisibleBoy { get; } = new LoadableResourceAsset($"{ShortPath}.RoleIcons.InvisibleBoy.png", 200);

    // Impostors (placeholder icons reusing ATR button art until proper TOU-style icons are made,
    // see docs/porting/README.md)
    public static LoadableAsset<Sprite> Astral { get; } = new LoadableResourceAsset($"{ShortPath}.RoleIcons.Astral.png", 200);
}