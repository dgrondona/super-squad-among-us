using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options;

/// <summary>
/// Tuning for abilities that a role can be GRANTED at runtime (Gooper's goop tiers, Kirby's swallow
/// inheritance - see <see cref="SuperSquadAmongUs.Modules.AbilityGrants"/>). These are deliberately a
/// single shared group rather than per-granting-role options: a granted Snipe is the same Snipe no
/// matter who unlocked it, and the role-agnostic granted buttons
/// (the <c>Granted*Button</c> family in Buttons/GrantedAbilityButtons.cs) have no single owning role to read
/// per-role options from. A standalone <see cref="AbstractOptionGroup"/> (like TOU-Mira's own
/// VanillaTweakOptions) rather than the role-tied <c>AbstractOptionGroup&lt;TRole&gt;</c>.
/// </summary>
public sealed class GrantedAbilityOptions : AbstractOptionGroup
{
    public override string GroupName => TouLocale.Get("SuperSquadGrantedAbilities", "Granted Abilities");

    public ModdedNumberOption KillCooldown { get; set; } =
        new("SuperSquadOptionGrantedKillCooldown", 25f, 5f, 120f, 2.5f, MiraNumberSuffixes.Seconds);

    public ModdedNumberOption SnipeCooldown { get; set; } =
        new("SuperSquadOptionGrantedSnipeCooldown", 25f, 10f, 60f, 2.5f, MiraNumberSuffixes.Seconds);

    public ModdedNumberOption AimWindow { get; set; } =
        new("SuperSquadOptionGrantedAimWindow", 10f, 5f, 30f, 1f, MiraNumberSuffixes.Seconds);

    public ModdedNumberOption SwoopCooldown { get; set; } =
        new("SuperSquadOptionGrantedSwoopCooldown", 25f, 5f, 60f, 2.5f, MiraNumberSuffixes.Seconds);

    public ModdedNumberOption SwoopDuration { get; set; } =
        new("SuperSquadOptionGrantedSwoopDuration", 10f, 3f, 30f, 1f, MiraNumberSuffixes.Seconds);

    public ModdedNumberOption VestCooldown { get; set; } =
        new("SuperSquadOptionGrantedVestCooldown", 30f, 15f, 90f, 2.5f, MiraNumberSuffixes.Seconds);

    public ModdedNumberOption VestDuration { get; set; } =
        new("SuperSquadOptionGrantedVestDuration", 15f, 5f, 60f, 2.5f, MiraNumberSuffixes.Seconds);

    public ModdedNumberOption HideCooldown { get; set; } =
        new("SuperSquadOptionGrantedHideCooldown", 25f, 5f, 60f, 2.5f, MiraNumberSuffixes.Seconds);

    // No HideDuration here: the granted Hide reuses Daddy Hagrid's CloakHiddenModifier verbatim, so its
    // duration is the Daddy Hagrid role's own HideDuration option (it IS Hagrid's ability).
}
