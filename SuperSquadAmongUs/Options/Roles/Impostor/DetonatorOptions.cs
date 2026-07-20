using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Impostor;

public sealed class DetonatorOptions : AbstractOptionGroup<DetonatorRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleDetonator", "Detonator");

    [ModdedNumberOption("SuperSquadOptionDetonatorAttachCooldown", 5f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float AttachCooldown { get; set; } = 25f;

    [ModdedNumberOption("SuperSquadOptionDetonatorArmDelay", 2f, 20f, 1f, MiraNumberSuffixes.Seconds)]
    public float ArmDelay { get; set; } = 5f;

    public ModdedNumberOption BlastRadius { get; set; } = new("SuperSquadOptionDetonatorBlastRadius", 0.15f, 0.05f, 0.5f,
        0.05f, MiraNumberSuffixes.Multiplier, "0.00");

    [ModdedToggleOption("SuperSquadOptionDetonatorCanKillImpostors")]
    public bool CanKillImpostors { get; set; }

    [ModdedToggleOption("SuperSquadOptionDetonatorCanVent")]
    public bool CanVent { get; set; } = true;
}
