using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Modifiers;
using TownOfUs.Modules.Localization;
using TownOfUs.Options;
using UnityEngine;

namespace SuperSquadAmongUs.Options.Modifiers;

/// <summary>
/// Ability tuning for the <see cref="SlideTackleModifier"/>. Assignment amount/chance live in
/// <see cref="SuperSquadModifierOptions"/>.
/// </summary>
public sealed class SlideTackleOptions : AbstractOptionGroup<SlideTackleModifier>
{
    /// <inheritdoc />
    public override Func<bool> GroupVisible => () => OptionGroupSingleton<RoleOptions>.Instance.IsClassicRoleAssignment;

    /// <inheritdoc />
    public override string GroupName => TouLocale.Get("SuperSquadModifierSlideTackle", "Slide Tackle");

    /// <inheritdoc />
    public override Color GroupColor => SuperSquadColors.SlideTackle;

    [ModdedNumberOption("SuperSquadOptionSlideTackleCooldown", 10f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float Cooldown { get; set; } = 30f;

    [ModdedNumberOption("SuperSquadOptionSlideTackleStunDuration", 0.5f, 10f, 0.5f, MiraNumberSuffixes.Seconds)]
    public float StunDuration { get; set; } = 2f;
}
