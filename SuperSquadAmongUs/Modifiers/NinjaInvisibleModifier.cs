using MiraAPI.GameOptions;
using SuperSquadAmongUs.Options.Roles.Impostor;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// The Ninja's post-assassination invisibility: applied the moment the assassinate goes through so the
/// Ninja can slip away from the kill scene unseen.
/// </summary>
public sealed class NinjaInvisibleModifier : TimedInvisibilityModifier
{
    /// <inheritdoc />
    public override string ModifierName => "Ninja Invisible";

    /// <inheritdoc />
    public override float Duration => OptionGroupSingleton<NinjaOptions>.Instance.InvisibleDuration;

    /// <inheritdoc />
    public override string GetDescription()
    {
        return "You are invisible after your assassination!";
    }
}
