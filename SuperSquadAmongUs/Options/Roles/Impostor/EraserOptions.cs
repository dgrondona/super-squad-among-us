using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Impostor;

public sealed class EraserOptions : AbstractOptionGroup<EraserRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleEraser", "Eraser");

    [ModdedNumberOption("SuperSquadOptionEraserEraseCooldown", 10f, 120f, 5f, MiraNumberSuffixes.Seconds)]
    public float EraseCooldown { get; set; } = 30f;

    /// <summary>
    /// When false, only crewmates can be targeted; when true, impostors and neutrals too.
    /// </summary>
    [ModdedToggleOption("SuperSquadOptionEraserCanEraseAnyone")]
    public bool CanEraseAnyone { get; set; }
}
