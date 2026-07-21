using MiraAPI.GameOptions;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using SuperSquadAmongUs.Roles.Impostor;
using SuperSquadAmongUs.Roles.Neutral;
using TownOfUs.Options;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// Synced dead-body removal for the Mafia Janitor's clean and the Vulture's eat (both TOR ports).
/// TOU-Mira's own <c>JanitorRole.RpcCleanBody</c> can't be reused - it validates the sender is a
/// TOU Janitor. These destroy the body outright (TOR behavior); known gap: TOU's Time Lord rewind
/// won't restore bodies removed here.
/// </summary>
public static class SuperSquadBodies
{
    /// <summary>
    /// Removes a dead body on every client (Mafia Janitor's clean).
    /// </summary>
    /// <param name="source">The Mafia Janitor.</param>
    /// <param name="parentId">The dead player's id (<see cref="DeadBody.ParentId"/>).</param>
    [MethodRpc((uint)SuperSquadRpc.MafiaCleanBody, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcMafiaCleanBody(PlayerControl source, byte parentId)
    {
        if (!AbilityGrants.SenderIsOrHolds<MafiaJanitorRole>(source))
        {
            Error("RpcMafiaCleanBody - Invalid mafia janitor");
            return;
        }

        DestroyBodies(parentId);
    }

    /// <summary>
    /// Removes a dead body on every client and counts it toward the Vulture's win. The count updates
    /// on every client (including the host, whose game-flow check ends the game when the threshold is
    /// reached - see <see cref="VultureRole.WinConditionMet"/>).
    /// </summary>
    /// <param name="source">The Vulture.</param>
    /// <param name="parentId">The dead player's id (<see cref="DeadBody.ParentId"/>).</param>
    [MethodRpc((uint)SuperSquadRpc.VultureEatBody, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcVultureEat(PlayerControl source, byte parentId)
    {
        if (!AbilityGrants.SenderIsOrHolds<VultureRole>(source))
        {
            Error("RpcVultureEat - Invalid vulture");
            return;
        }

        DestroyBodies(parentId);

        // Only a real Vulture advances its win counter - a role that borrowed the eat kit
        // (AbilityGrants.GrantedKits) gets the body-removal utility but no Vulture win progress.
        if (source.Data.Role is VultureRole vulture)
        {
            vulture.EatenBodies++;
        }
    }

    private static void DestroyBodies(byte parentId)
    {
        // Same pet-hiding rule TOU-Mira's own Janitor/Chef clean uses (Extensions.CoClean) - reuses
        // TOU's own VanillaTweakOptions.PetVisibilityUponDeath now that the pinned package has it
        // (previously reimplemented manually - see docs/il2cpp-gotchas.md).
        var hidden = OptionGroupSingleton<VanillaTweakOptions>.Instance.PetVisibilityUponDeath;
        if (hidden != PetHidden.Never)
        {
            var player = MiscUtils.PlayerById(parentId);
            if (player != null && !player.AmOwner)
            {
                MiscUtils.RemovePet(player, hidden);
            }
        }

        foreach (var body in UnityEngine.Object.FindObjectsOfType<DeadBody>())
        {
            if (body.ParentId == parentId)
            {
                UnityEngine.Object.Destroy(body.gameObject);
            }
        }
    }
}
