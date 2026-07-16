using MiraAPI.GameOptions;
using SuperSquadAmongUs.Options.Roles.Impostor;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// The Astral's post-return grace phase: after snapping back to the phase spot the player stays
/// invisible for a few more seconds (normal collision) so returning isn't a dead giveaway.
/// </summary>
public sealed class AstralLingerModifier : AstralInvisibilityModifier
{
    /// <inheritdoc />
    public override string ModifierName => "Astral Linger";

    /// <inheritdoc />
    public override float Duration => OptionGroupSingleton<AstralOptions>.Instance.LingerDuration;

    /// <inheritdoc />
    public override string GetDescription()
    {
        return "You returned to your body but remain unseen briefly!";
    }
}
