using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Neutral;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Neutral;

public sealed class PelicanOptions : AbstractOptionGroup<PelicanRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRolePelican", "Pelican");

    [ModdedNumberOption("SuperSquadOptionPelicanDevourCooldown", 10f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float DevourCooldown { get; set; } = 15f;

    [ModdedToggleOption("SuperSquadOptionPelicanCanVent")]
    public bool CanVent { get; set; }

    [ModdedToggleOption("SuperSquadOptionPelicanImpostorVision")]
    public bool ImpostorVision { get; set; } = true;
}
