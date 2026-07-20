using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Crewmate;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Crewmate;

public sealed class SuiOptions : AbstractOptionGroup<SuiRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleSui", "Sui");

    [ModdedNumberOption("SuperSquadOptionSuiProtectCooldown", 5f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float ProtectCooldown { get; set; } = 20f;

    // Only meaningful combined with CanTargetAnyone - with it off, the retaliation target is always
    // the actual interactor, so there's no "wrong person" to pick.
    [ModdedToggleOption("SuperSquadOptionSuiCanTargetAnyone")]
    public bool CanTargetAnyone { get; set; }

    [ModdedToggleOption("SuperSquadOptionSuiDiesOnWrongTarget")]
    public bool DiesOnWrongTarget { get; set; }

    [ModdedToggleOption("SuperSquadOptionSuiOnlyImpostorKillsTrigger")]
    public bool OnlyImpostorKillsTrigger { get; set; }
}
