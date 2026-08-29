using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Crewmate;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Crewmate;

public sealed class ElusiveOptions : AbstractRoleOptionGroup<ElusiveRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleElusive", "Elusive");

    [ModdedNumberOption("SuperSquadOptionElusiveShieldCooldown", 5f, 120f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float ShieldCooldown { get; set; } = 25f;

    [ModdedNumberOption("SuperSquadOptionElusiveShieldDuration", 5f, 15f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float ShieldDuration { get; set; } = 10f;

    [ModdedNumberOption("SuperSquadOptionElusiveMaxShields", 1f, 15f, 1f, MiraNumberSuffixes.None, "0")]
    public float MaxShields { get; set; } = 5f;
}
