using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Impostor;

public sealed class WitchOptions : AbstractOptionGroup<WitchRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleWitch", "Witch");

    [ModdedNumberOption("SuperSquadOptionWitchHexCooldown", 10f, 120f, 5f, MiraNumberSuffixes.Seconds)]
    public float HexCooldown { get; set; } = 30f;

    [ModdedNumberOption("SuperSquadOptionWitchAdditionalCooldown", 0f, 60f, 5f, MiraNumberSuffixes.Seconds)]
    public float AdditionalCooldown { get; set; } = 10f;

    [ModdedToggleOption("SuperSquadOptionWitchCanHexAnyone")]
    public bool CanHexAnyone { get; set; }

    [ModdedNumberOption("SuperSquadOptionWitchCastDuration", 0f, 10f, 1f, MiraNumberSuffixes.Seconds)]
    public float CastDuration { get; set; } = 1f;

    [ModdedToggleOption("SuperSquadOptionWitchTriggerBothCooldowns")]
    public bool TriggerBothCooldowns { get; set; } = true;

    [ModdedToggleOption("SuperSquadOptionWitchVotingWitchSavesTargets")]
    public bool VotingWitchSavesTargets { get; set; } = true;

    [ModdedToggleOption("SuperSquadOptionWitchCanVent")]
    public bool CanVent { get; set; } = true;
}
