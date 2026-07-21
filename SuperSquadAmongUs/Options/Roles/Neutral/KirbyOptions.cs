using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Neutral;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Neutral;

public sealed class KirbyOptions : AbstractOptionGroup<KirbyRole>
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleKirby", "Kirby");

    [ModdedNumberOption("SuperSquadOptionKirbySwallowCooldown", 10f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float SwallowCooldown { get; set; } = 15f;

    [ModdedToggleOption("SuperSquadOptionKirbyCanVent")]
    public bool CanVent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether swallowed abilities stack (default) or each new swallow
    /// replaces whatever the previous ones granted.
    /// </summary>
    [ModdedToggleOption("SuperSquadOptionKirbyAccumulate")]
    public bool AccumulateAbilities { get; set; } = true;

    [ModdedToggleOption("SuperSquadOptionKirbyImpostorVision")]
    public bool ImpostorVision { get; set; } = true;

    // Cooldowns/durations for inherited kill/snipe/swoop abilities live in the shared
    // GrantedAbilityOptions group, not here.
}
