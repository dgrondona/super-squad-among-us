using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Translation;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Neutral;

namespace SuperSquadAmongUs.Options.Roles.Neutral;

public sealed class GooperOptions : AbstractRoleOptionGroup<GooperRole>
{
    public override string GroupName => MiraLocaleManager.Get("SuperSquadRoleGooper", "Gooper");

    [ModdedNumberOption("SuperSquadOptionGooperGoopCooldown", 5f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float GoopCooldown { get; set; } = 20f;

    // The vest/kill/snipe/swoop cooldowns and durations live in the shared GrantedAbilityOptions group
    // (a granted ability is the same ability whoever unlocked it), not here.
}
