using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Impostor;

public sealed class DumperOptions : AbstractOptionGroup<DumperRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleDumper", "Dumper");

    [ModdedNumberOption("SuperSquadOptionDumperCarryCooldown", 5f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float CarryCooldown { get; set; } = 20f;

    [ModdedNumberOption("SuperSquadOptionDumperCarryDuration", 5f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float CarryDuration { get; set; } = 20f;

    [ModdedToggleOption("SuperSquadOptionDumperCanVent")]
    public bool CanVent { get; set; } = true;
}
