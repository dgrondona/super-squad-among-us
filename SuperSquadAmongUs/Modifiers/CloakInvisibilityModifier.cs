using MiraAPI.GameOptions;
using SuperSquadAmongUs.Options.Modifiers;
using TownOfUs.Options;
using TownOfUs.Utilities;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// The timed invisibility effect applied when an <see cref="InvisibilityCloakModifier"/> holder uses
/// their Cloak button. Unlike the impostor phases sharing <see cref="TimedInvisibilityModifier"/>, the
/// cloak can be worn by anyone, so impostors get no outline - only the wearer (and the informed dead)
/// see the faint silhouette.
/// </summary>
public sealed class CloakInvisibilityModifier : TimedInvisibilityModifier
{
    /// <inheritdoc />
    public override string ModifierName => "Cloaked";

    /// <inheritdoc />
    public override float Duration => OptionGroupSingleton<InvisibilityCloakOptions>.Instance.Duration;

    /// <inheritdoc />
    public override string GetDescription() => "You are hidden under your invisibility cloak!";

    /// <inheritdoc />
    protected override bool LocalViewerSeesOutline()
    {
        return Player.AmOwner ||
               (PlayerControl.LocalPlayer &&
                PlayerControl.LocalPlayer.DiedOtherRound() &&
                OptionGroupSingleton<GeneralOptions>.Instance.TheDeadKnow);
    }
}
