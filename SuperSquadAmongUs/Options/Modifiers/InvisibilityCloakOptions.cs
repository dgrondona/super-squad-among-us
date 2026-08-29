using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Modifiers;
using TownOfUs.Modules.Localization;
using TownOfUs.Options;
using UnityEngine;

namespace SuperSquadAmongUs.Options.Modifiers;

/// <summary>
/// Ability tuning for the <see cref="InvisibilityCloakModifier"/>. Assignment amount/chance live in
/// <see cref="SuperSquadModifierOptions"/>.
/// </summary>
public sealed class InvisibilityCloakOptions : AbstractOptionGroup<InvisibilityCloakModifier>
{
    /// <inheritdoc />
    public override MenuCategory ParentMenu => MenuCategory.Modifiers;

    /// <inheritdoc />
    public override Func<bool> GroupVisible => () => OptionGroupSingleton<RoleOptions>.Instance.IsClassicRoleAssignment;

    /// <inheritdoc />
    public override string GroupName => TouLocale.Get("SuperSquadModifierInvisibilityCloak", "Invisibility Cloak");

    /// <inheritdoc />
    public override Color GroupColor => SuperSquadColors.InvisibilityCloak;

    [ModdedNumberOption("SuperSquadOptionInvisibilityCloakCooldown", 10f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float Cooldown { get; set; } = 25f;

    [ModdedNumberOption("SuperSquadOptionInvisibilityCloakDuration", 2.5f, 15f, 0.5f, MiraNumberSuffixes.Seconds)]
    public float Duration { get; set; } = 6f;
}
