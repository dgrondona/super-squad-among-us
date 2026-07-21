using MiraAPI.GameOptions;
using MiraAPI.GameOptions.OptionTypes;
using MiraAPI.Utilities;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options;

/// <summary>
/// Tuning for the granted ability PRIMITIVES (see <see cref="SuperSquadAmongUs.Modules.AbilityGrants"/>:
/// the flag abilities with no borrowable source button - Kill, Swoop, Vest). Deliberately a single
/// shared group rather than per-granting-role options: a granted Swoop is the same Swoop no matter who
/// unlocked it, and the <c>Granted*Button</c> primitives have no single owning role to read per-role
/// options from. Abilities borrowed as whole kits (Snipe, Hide, and every other role of this addon)
/// use the SOURCE role's own options - they are the source role's real buttons. A standalone
/// <see cref="AbstractOptionGroup"/> (like TOU-Mira's own VanillaTweakOptions) rather than the
/// role-tied <c>AbstractOptionGroup&lt;TRole&gt;</c>.
/// </summary>
public sealed class GrantedAbilityOptions : AbstractOptionGroup
{
    public override string GroupName => TouLocale.Get("SuperSquadGrantedAbilities", "Granted Abilities");

    public ModdedNumberOption KillCooldown { get; set; } =
        new("SuperSquadOptionGrantedKillCooldown", 25f, 5f, 120f, 2.5f, MiraNumberSuffixes.Seconds);

    public ModdedNumberOption SwoopCooldown { get; set; } =
        new("SuperSquadOptionGrantedSwoopCooldown", 25f, 5f, 60f, 2.5f, MiraNumberSuffixes.Seconds);

    public ModdedNumberOption SwoopDuration { get; set; } =
        new("SuperSquadOptionGrantedSwoopDuration", 10f, 3f, 30f, 1f, MiraNumberSuffixes.Seconds);

    public ModdedNumberOption VestCooldown { get; set; } =
        new("SuperSquadOptionGrantedVestCooldown", 30f, 15f, 90f, 2.5f, MiraNumberSuffixes.Seconds);

    public ModdedNumberOption VestDuration { get; set; } =
        new("SuperSquadOptionGrantedVestDuration", 15f, 5f, 60f, 2.5f, MiraNumberSuffixes.Seconds);
}
