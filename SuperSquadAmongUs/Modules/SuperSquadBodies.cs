using MiraAPI.GameOptions;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
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
        DestroyBodies(parentId);

        if (source != null && source.Data?.Role is VultureRole vulture)
        {
            vulture.EatenBodies++;
        }
    }

    private static void DestroyBodies(byte parentId)
    {
        // Same pet-hiding rule TOU-Mira's own Janitor/Chef clean uses (Extensions.CoClean), reimplemented
        // here since that method is body-owned and private: only strip the pet if the host both enabled
        // "Remove Pets Upon Janitor/Chef Clean" and set pet visibility to Always Visible (the option is
        // moot under the other visibility modes).
        var petOptions = OptionGroupSingleton<VanillaTweakOptions>.Instance;
        if (petOptions.HidePetsOnBodyRemove.Value && petOptions.ShowPetsMode.Value == (int)PetVisiblity.AlwaysVisible)
        {
            var player = MiscUtils.PlayerById(parentId);
            if (player != null && !player.AmOwner)
            {
                MiscUtils.RemovePet(player);
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
