using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Neutral;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Neutral;

public sealed class VultureOptions : AbstractRoleOptionGroup<VultureRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleVulture", "Vulture");

    [ModdedNumberOption("SuperSquadOptionVultureEatCooldown", 10f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float EatCooldown { get; set; } = 15f;

    [ModdedNumberOption("SuperSquadOptionVultureBodiesNeededToWin", 1f, 10f, 1f, MiraNumberSuffixes.None)]
    public float BodiesNeededToWin { get; set; } = 4f;

    [ModdedToggleOption("SuperSquadOptionVultureCanVent")]
    public bool CanVent { get; set; } = true;

    [ModdedToggleOption("SuperSquadOptionVultureShowBodyArrows")]
    public bool ShowBodyArrows { get; set; } = true;
}
