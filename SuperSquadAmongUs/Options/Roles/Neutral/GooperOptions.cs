using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Neutral;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Neutral;

public sealed class GooperOptions : AbstractOptionGroup<GooperRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleGooper", "Gooper");

    [ModdedNumberOption("SuperSquadOptionGooperGoopCooldown", 5f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float GoopCooldown { get; set; } = 20f;

    [ModdedNumberOption("SuperSquadOptionGooperVestDuration", 5f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float VestDuration { get; set; } = 15f;

    [ModdedNumberOption("SuperSquadOptionGooperVestCooldown", 15f, 90f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float VestCooldown { get; set; } = 30f;

    [ModdedNumberOption("SuperSquadOptionGooperKillCooldown", 5f, 120f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float KillCooldown { get; set; } = 25f;

    [ModdedNumberOption("SuperSquadOptionGooperSnipeCooldown", 10f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float SnipeCooldown { get; set; } = 25f;

    [ModdedNumberOption("SuperSquadOptionGooperAimWindow", 5f, 30f, 1f, MiraNumberSuffixes.Seconds)]
    public float AimWindow { get; set; } = 10f;

    [ModdedNumberOption("SuperSquadOptionGooperSwoopCooldown", 5f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float SwoopCooldown { get; set; } = 25f;

    [ModdedNumberOption("SuperSquadOptionGooperSwoopDuration", 3f, 30f, 1f, MiraNumberSuffixes.Seconds)]
    public float SwoopDuration { get; set; } = 10f;
}
