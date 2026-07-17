using MiraAPI.Utilities.Assets;
using UnityEngine;

namespace SuperSquadAmongUs.Assets;

public static class SuperSquadRoleIcons
{
    // THIS FILE SHOULD ONLY HOLD ROLE ICONS

    private const string ShortPath = "SuperSquadAmongUs.Resources";

    // Neutrals
    public static LoadableAsset<Sprite> Sentinel { get; } = new LoadableResourceAsset($"{ShortPath}.RoleIcons.Sentinel.png", 200);
    public static LoadableAsset<Sprite> Vulture { get; } = new LoadableResourceAsset($"{ShortPath}.RoleIcons.Vulture.png", 200);

    // Pelican has no dedicated icon yet - uses the generic Neutral placeholder (see SuperSquadAssets).
    public static LoadableAsset<Sprite> Pelican => SuperSquadAssets.NeutralPlaceholderIcon;

    // Crewmates
    public static LoadableAsset<Sprite> Apparater { get; } = new LoadableResourceAsset($"{ShortPath}.RoleIcons.Apparater.png", 200);
    public static LoadableAsset<Sprite> InvisibleBoy { get; } = new LoadableResourceAsset($"{ShortPath}.RoleIcons.InvisibleBoy.png", 200);

    // Daddy Hagrid and the Elusive have no dedicated icons yet - they use the generic Crewmate
    // placeholder (see SuperSquadAssets).
    public static LoadableAsset<Sprite> DaddyHagrid => SuperSquadAssets.CrewmatePlaceholderIcon;
    public static LoadableAsset<Sprite> Elusive => SuperSquadAssets.CrewmatePlaceholderIcon;

    // Impostors
    public static LoadableAsset<Sprite> Witch { get; } = new LoadableResourceAsset($"{ShortPath}.RoleIcons.Witch.png", 200);
    public static LoadableAsset<Sprite> Godfather { get; } = new LoadableResourceAsset($"{ShortPath}.RoleIcons.Godfather.png", 200);
    public static LoadableAsset<Sprite> Mafioso { get; } = new LoadableResourceAsset($"{ShortPath}.RoleIcons.Mafioso.png", 200);
    public static LoadableAsset<Sprite> MafiaJanitor { get; } = new LoadableResourceAsset($"{ShortPath}.RoleIcons.MafiaJanitor.png", 200);
    public static LoadableAsset<Sprite> Eraser { get; } = new LoadableResourceAsset($"{ShortPath}.RoleIcons.Eraser.png", 200);

    // Astral, Ninja, and Sniper have no dedicated icon yet - use the generic Impostor placeholder
    // (see SuperSquadAssets) instead of the old AI-generated art.
    public static LoadableAsset<Sprite> Astral => SuperSquadAssets.ImpostorPlaceholderIcon;
    public static LoadableAsset<Sprite> Ninja => SuperSquadAssets.ImpostorPlaceholderIcon;
    public static LoadableAsset<Sprite> Sniper => SuperSquadAssets.ImpostorPlaceholderIcon;
}