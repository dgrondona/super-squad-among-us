using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Crewmate;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Crewmate;

public sealed class ApparaterOptions : AbstractOptionGroup<ApparaterRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleApparater", "Apparater");

    [ModdedNumberOption("SuperSquadOptionApparaterCooldown", 10f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float TeleportCooldown { get; set; } = 30f;

    [ModdedNumberOption("SuperSquadOptionApparaterSelectTime", 3f, 20f, 1f, MiraNumberSuffixes.Seconds)]
    public float SelectTime { get; set; } = 10f;

    [ModdedNumberOption("SuperSquadOptionApparaterMaxUses", 1f, 15f, 1f, MiraNumberSuffixes.None, "0")]
    public float MaxUses { get; set; } = 5f;
}
