using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Impostor;

public sealed class EraserOptions : AbstractRoleOptionGroup<EraserRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleEraser", "Eraser");

    [ModdedNumberOption("SuperSquadOptionEraserEraseCooldown", 10f, 120f, 5f, MiraNumberSuffixes.Seconds)]
    public float EraseCooldown { get; set; } = 30f;

    // -1 is the infinite-uses sentinel here, not 0 - see ApparaterOptions.MaxUses for the same convention.
    public ModdedNumberOption MaxUses { get; } =
        new("SuperSquadOptionEraserMaxUses", 2f, -1f, 15f, 1f, "0", "∞", MiraNumberSuffixes.None, "0");

    /// <summary>
    /// When false, only crewmates can be targeted; when true, impostors and neutrals too.
    /// </summary>
    [ModdedToggleOption("SuperSquadOptionEraserCanEraseAnyone")]
    public bool CanEraseAnyone { get; set; }

    /// <summary>
    /// When false (default), erased players lose their role at the next meeting's exile screen.
    /// When true, the role is stripped the instant the erase is used.
    /// </summary>
    [ModdedToggleOption("SuperSquadOptionEraserImmediate")]
    public bool EraseImmediately { get; set; }
}
