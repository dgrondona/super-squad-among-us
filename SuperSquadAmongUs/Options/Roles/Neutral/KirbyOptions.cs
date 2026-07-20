using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Neutral;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Neutral;

public sealed class KirbyOptions : AbstractOptionGroup<KirbyRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleKirby", "Kirby");

    [ModdedNumberOption("SuperSquadOptionKirbySwallowCooldown", 10f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float SwallowCooldown { get; set; } = 15f;

    [ModdedToggleOption("SuperSquadOptionKirbyCanVent")]
    public bool CanVent { get; set; }

    [ModdedToggleOption("SuperSquadOptionKirbyImpostorVision")]
    public bool ImpostorVision { get; set; } = true;

    [ModdedNumberOption("SuperSquadOptionKirbyKillCooldown", 5f, 120f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float KillCooldown { get; set; } = 25f;

    [ModdedNumberOption("SuperSquadOptionKirbySnipeCooldown", 10f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float SnipeCooldown { get; set; } = 25f;

    [ModdedNumberOption("SuperSquadOptionKirbyAimWindow", 5f, 30f, 1f, MiraNumberSuffixes.Seconds)]
    public float AimWindow { get; set; } = 10f;

    [ModdedNumberOption("SuperSquadOptionKirbySwoopCooldown", 5f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float SwoopCooldown { get; set; } = 25f;

    [ModdedNumberOption("SuperSquadOptionKirbySwoopDuration", 3f, 30f, 1f, MiraNumberSuffixes.Seconds)]
    public float SwoopDuration { get; set; } = 10f;
}
