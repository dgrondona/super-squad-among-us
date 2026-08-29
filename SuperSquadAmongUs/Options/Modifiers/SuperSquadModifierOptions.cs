using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;

namespace SuperSquadAmongUs.Options.Modifiers;

/// <summary>
/// Shared assignment options (amount + chance) for this addon's universal game modifiers, shown in
/// TOU-Mira's Modifiers settings tab alongside its own "Universal Modifiers" group. Per-modifier
/// ability tuning lives in each modifier's own option group (e.g.
/// <see cref="InvisibilityCloakOptions"/>).
/// </summary>
public sealed class SuperSquadModifierOptions : AbstractOptionGroup
{
    /// <inheritdoc />
    public override string GroupName => "Super Squad Modifiers";

    /// <inheritdoc />
    public override MenuCategory ParentMenu => MenuCategory.Modifiers;

    /// <inheritdoc />
    public override bool ShowInModifiersMenu => true;

    /// <inheritdoc />
    public override uint GroupPriority => 36;

    [ModdedNumberOption("SuperSquadOptionInvisibilityCloakAmount", 0f, 5f, 1f)]
    public float InvisibilityCloakAmount { get; set; } = 0f;

    public ModdedNumberOption InvisibilityCloakChance { get; } =
        new("SuperSquadOptionInvisibilityCloakChance", 50f, 0f, 100f, 10f, MiraNumberSuffixes.Percent)
        {
            Visible = () => OptionGroupSingleton<SuperSquadModifierOptions>.Instance.InvisibilityCloakAmount > 0,
        };

    [ModdedNumberOption("SuperSquadOptionSlideTackleAmount", 0f, 5f, 1f)]
    public float SlideTackleAmount { get; set; } = 0f;

    public ModdedNumberOption SlideTackleChance { get; } =
        new("SuperSquadOptionSlideTackleChance", 50f, 0f, 100f, 10f, MiraNumberSuffixes.Percent)
        {
            Visible = () => OptionGroupSingleton<SuperSquadModifierOptions>.Instance.SlideTackleAmount > 0,
        };
}
