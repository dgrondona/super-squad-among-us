namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// The "in the Pelican's stomach" state: all mechanics live in <see cref="CarriedModifier"/> (hidden,
/// frozen, pinned to the Pelican). Removed with a release when the Pelican is killed mid-round;
/// devoured players die at the start of the next meeting (see Events/PelicanEvents.cs). Indefinite:
/// inherits ConcealedModifier's AutoStart=false so the timer never runs.
/// </summary>
public sealed class DevouredModifier(PlayerControl pelican) : CarriedModifier(pelican)
{
    /// <summary>
    /// Gets the Pelican that devoured this player.
    /// </summary>
    public PlayerControl Pelican => Carrier;

    /// <inheritdoc />
    public override string ModifierName => "Devoured";

    /// <inheritdoc />
    public override string GetDescription()
    {
        return "You have been devoured by the Pelican!";
    }
}
