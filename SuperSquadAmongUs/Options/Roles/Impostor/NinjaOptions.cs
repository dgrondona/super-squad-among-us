using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Impostor;

public sealed class NinjaOptions : AbstractRoleOptionGroup<NinjaRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleNinja", "Ninja");

    [ModdedNumberOption("SuperSquadOptionNinjaMarkCooldown", 10f, 120f, 5f, MiraNumberSuffixes.Seconds)]
    public float MarkCooldown { get; set; } = 30f;

    [ModdedToggleOption("SuperSquadOptionNinjaKnowsTargetLocation")]
    public bool KnowsTargetLocation { get; set; } = true;

    [ModdedNumberOption("SuperSquadOptionNinjaTraceDuration", 1f, 20f, 0.5f, MiraNumberSuffixes.Seconds)]
    public float TraceDuration { get; set; } = 5f;

    [ModdedNumberOption("SuperSquadOptionNinjaTraceColorFadeDuration", 0f, 20f, 0.5f, MiraNumberSuffixes.Seconds)]
    public float TraceColorFadeDuration { get; set; } = 2f;

    [ModdedNumberOption("SuperSquadOptionNinjaInvisibleDuration", 0f, 20f, 1f, MiraNumberSuffixes.Seconds)]
    public float InvisibleDuration { get; set; } = 3f;

    [ModdedToggleOption("SuperSquadOptionNinjaCanVent")]
    public bool CanVent { get; set; }
}
