using SuperSquadAmongUs.Modules;
using SuperSquadAmongUs.Roles.Neutral;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// The "swallowed by Kirby" state: all mechanics live in <see cref="CarriedModifier"/> (hidden, frozen,
/// pinned to Kirby) - a near-literal copy of the Pelican's stomach (<see cref="DevouredModifier"/>),
/// per the brief's own "swallow players like the pelican" instruction. A new **sealed sibling**, never
/// subclassed from <see cref="DevouredModifier"/>: <see cref="Events.PelicanEvents"/> and
/// <see cref="Events.KirbyEvents"/> each iterate their own concrete modifier type, and subclassing one
/// from the other would make each catch the other's carried players. Indefinite: inherits
/// ConcealedModifier's AutoStart=false so the timer never runs - digested at the next meeting
/// (<see cref="Events.KirbyEvents"/>) or released alive if Kirby dies first, exactly like the Pelican.
/// </summary>
public sealed class KirbySwallowedModifier(PlayerControl kirby) : CarriedModifier(kirby)
{
    /// <summary>
    /// Gets the Kirby that swallowed this player.
    /// </summary>
    public PlayerControl Kirby => Carrier;

    /// <inheritdoc />
    public override string ModifierName => "Swallowed";

    /// <inheritdoc />
    public override void OnActivate()
    {
        base.OnActivate();

        // Kirby inherits the victim's portable abilities the instant they're swallowed (per the user's
        // request), not at digestion. Runs on every client because the modifier add is the synced call,
        // so the grant stays deterministic - the same local-mutation pattern the Vulture eat count uses.
        if (Kirby?.Data?.Role is KirbyRole kirbyRole && Player?.Data != null)
        {
            kirbyRole.UnlockedAbilities |= AbilityGrants.GetPortableAbilities(Player.Data.Role);

            // Surface venting (cached bool + vent button) if this swallow just granted it - see
            // AbilityGrants.EnableVenting.
            if (kirbyRole.UnlockedAbilities.HasFlag(GrantableAbility.Vent))
            {
                AbilityGrants.EnableVenting(kirbyRole);
            }
        }
    }

    /// <inheritdoc />
    public override string GetDescription()
    {
        return "You have been swallowed by Kirby!";
    }
}
