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
        // request), not at digestion. Runs on every client (this modifier is the synced add), so the
        // grant stays deterministic - same reasoning as VultureRole.EatenBodies++.
        if (Kirby?.Data?.Role is KirbyRole kirbyRole && Player?.Data != null)
        {
            kirbyRole.UnlockedAbilities |= AbilityGrants.GetPortableAbilities(Player.Data.Role);

            // Keep the vanilla cached CanVent bool in step with the inherited Vent flag (see the same
            // note in SuperSquadGooper.RpcGoop).
            if (kirbyRole.UnlockedAbilities.HasFlag(GrantableAbility.Vent))
            {
                kirbyRole.CanVent = true;
            }
        }
    }

    /// <inheritdoc />
    public override string GetDescription()
    {
        return "You have been swallowed by Kirby!";
    }
}
