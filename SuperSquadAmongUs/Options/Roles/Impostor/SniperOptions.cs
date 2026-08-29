using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Impostor;

public sealed class SniperOptions : AbstractRoleOptionGroup<SniperRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleSniper", "Sniper");

    [ModdedNumberOption("SuperSquadOptionSniperSnipeCooldown", 10f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float SnipeCooldown { get; set; } = 25f;

    [ModdedNumberOption("SuperSquadOptionSniperAimWindow", 5f, 30f, 1f, MiraNumberSuffixes.Seconds)]
    public float AimWindow { get; set; } = 10f;

    [ModdedToggleOption("SuperSquadOptionSniperCanKillImpostors")]
    public bool CanKillImpostors { get; set; }

    [ModdedToggleOption("SuperSquadOptionSniperCanVent")]
    public bool CanVent { get; set; }
}
