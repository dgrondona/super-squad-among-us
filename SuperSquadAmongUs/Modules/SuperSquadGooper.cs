using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using SuperSquadAmongUs.Roles.Neutral;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// The Gooper's goop-tier escalation. Branches on how many bodies have been gooped so far, so it can't
/// be expressed as a single generic modifier add: 1st goop grants a protective vest, 2nd grants
/// permanent kill+vent, 3rd and beyond each grant one additional ability drawn (without replacement)
/// from <see cref="AbilityGrants.Pool"/>. See docs/roles/gooper.md.
/// </summary>
public static class SuperSquadGooper
{
    /// <summary>
    /// Applies one goop of <paramref name="bodyId"/> and its tier reward, if any. No-ops if that body
    /// was already gooped. <paramref name="chosenPoolIndex"/> (an index into
    /// <see cref="AbilityGrants.Pool"/>) is picked client-side by the Gooper before sending this RPC
    /// (<see cref="AbilityGrants.PickRandomPoolIndex"/>) so every client applies the identical draw.
    /// </summary>
    /// <param name="source">The Gooper.</param>
    /// <param name="bodyId">The gooped body's <c>DeadBody.ParentId</c>.</param>
    /// <param name="chosenPoolIndex">
    /// The pool entry to grant if this is the 3rd goop or later, or
    /// <see cref="AbilityGrants.NoPoolChoice"/> if the pool is already exhausted or this is only the
    /// 1st/2nd goop.
    /// </param>
    [MethodRpc((uint)SuperSquadRpc.GooperGoop, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcGoop(PlayerControl source, byte bodyId, byte chosenPoolIndex)
    {
        if (source.Data.Role is not GooperRole gooper)
        {
            Error("RpcGoop - Invalid Gooper");
            return;
        }

        if (!gooper.GoopedBodyIds.Add(bodyId))
        {
            return;
        }

        switch (gooper.GoopedBodyIds.Count)
        {
            case 1:
                // 1st goop unlocks the Vest *ability* (a button the Gooper triggers when they want the
                // shield), not an auto-applied modifier - see GrantedVestButton.
                gooper.UnlockedAbilities |= GrantableAbility.Vest;
                break;
            case 2:
                gooper.UnlockedAbilities |= GrantableAbility.Kill | GrantableAbility.Vent;
                AbilityGrants.SyncVenting(gooper);
                break;
            default:
                AbilityGrants.ApplyPoolGrant(gooper, chosenPoolIndex);
                break;
        }
    }
}
