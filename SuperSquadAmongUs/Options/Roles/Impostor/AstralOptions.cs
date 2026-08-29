using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Impostor;

public sealed class AstralOptions : AbstractRoleOptionGroup<AstralRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleAstral", "Astral");

    [ModdedNumberOption("SuperSquadOptionAstralFormCooldown", 10f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float FormCooldown { get; set; } = 25f;

    [ModdedNumberOption("SuperSquadOptionAstralFormDuration", 5f, 30f, 1f, MiraNumberSuffixes.Seconds)]
    public float FormDuration { get; set; } = 15f;

    [ModdedNumberOption("SuperSquadOptionAstralLingerDuration", 0f, 15f, 1f, MiraNumberSuffixes.Seconds)]
    public float LingerDuration { get; set; } = 5f;

    [ModdedToggleOption("SuperSquadOptionAstralCanVent")]
    public bool CanVent { get; set; }

    [ModdedToggleOption("SuperSquadOptionAstralDieWithoutKill")]
    public bool DieWithoutKill { get; set; }
}
