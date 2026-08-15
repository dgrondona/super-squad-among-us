using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.GameOptions.OptionTypes;
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

    // Hidden unless CanTargetAnyone is on - see the comment above.
    public ModdedToggleOption DiesOnWrongTarget { get; } = new("SuperSquadOptionSuiDiesOnWrongTarget", false)
    {
        Visible = () => OptionGroupSingleton<SuiOptions>.Instance.CanTargetAnyone,
    };

    [ModdedToggleOption("SuperSquadOptionSuiOnlyImpostorKillsTrigger")]
    public bool OnlyImpostorKillsTrigger { get; set; }
}
