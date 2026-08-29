using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Impostor;

public sealed class RcXdOptions : AbstractRoleOptionGroup<RcXdRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleRcXd", "RC-XD");

    [ModdedNumberOption("SuperSquadOptionRcXdDeployCooldown", 10f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float DeployCooldown { get; set; } = 25f;

    [ModdedNumberOption("SuperSquadOptionRcXdDriveTime", 2f, 15f, 1f, MiraNumberSuffixes.Seconds)]
    public float DriveTime { get; set; } = 8f;

    [ModdedNumberOption("SuperSquadOptionRcXdCarSpeed", 1f, 3f, 0.25f, MiraNumberSuffixes.Multiplier, "0.00")]
    public float CarSpeedMultiplier { get; set; } = 2f;

    public ModdedNumberOption ExplosionRadius { get; set; } = new("SuperSquadOptionRcXdExplosionRadius", 0.25f, 0.05f, 1f, 0.05f,
        MiraNumberSuffixes.Multiplier, "0.00");

    [ModdedToggleOption("SuperSquadOptionRcXdCanKillImpostors")]
    public bool CanKillImpostors { get; set; } = true;

    [ModdedToggleOption("SuperSquadOptionRcXdCanVent")]
    public bool CanVent { get; set; }
}
