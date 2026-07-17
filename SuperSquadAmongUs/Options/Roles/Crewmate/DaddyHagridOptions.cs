using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Crewmate;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Crewmate;

public sealed class DaddyHagridOptions : AbstractOptionGroup<DaddyHagridRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleDaddyHagrid", "Daddy Hagrid");

    [ModdedNumberOption("SuperSquadOptionDaddyHagridHideCooldown", 10f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float HideCooldown { get; set; } = 25f;

    [ModdedNumberOption("SuperSquadOptionDaddyHagridHideDuration", 5f, 30f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float HideDuration { get; set; } = 15f;

    // -1 is the infinite-uses sentinel here, not 0 - same convention as the Apparater.
    public ModdedNumberOption MaxUses { get; } =
        new("SuperSquadOptionDaddyHagridMaxUses", 3f, -1f, 15f, 1f, "0", "∞", MiraNumberSuffixes.None, "0");
}
