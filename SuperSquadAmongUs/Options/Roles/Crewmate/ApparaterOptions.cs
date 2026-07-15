using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Crewmate;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Crewmate;

public sealed class ApparaterOptions : AbstractOptionGroup<ApparaterRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleApparater", "Apparater");

    [ModdedNumberOption("SuperSquadOptionApparaterCooldown", 5f, 60f, 1f, MiraNumberSuffixes.Seconds)]
    public float TeleportCooldown { get; set; } = 6f;

    // -1 is the "infinite uses" sentinel here, not 0 - matches TownOfUsButton's default
    // ZeroIsInfinite=false (see TownOfUsButton.cs), which ApparaterMapButton doesn't override, and
    // mirrors the same pattern EngineerOptions.MaxVents/MaxFixes use for the same reason.
    public ModdedNumberOption MaxUses { get; } =
        new("SuperSquadOptionApparaterMaxUses", 4f, -1f, 15f, 1f, "0", "∞", MiraNumberSuffixes.None, "0");
}
