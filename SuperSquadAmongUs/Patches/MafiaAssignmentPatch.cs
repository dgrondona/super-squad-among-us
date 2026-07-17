using HarmonyLib;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Options.Roles.Impostor;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs.Utilities;

namespace SuperSquadAmongUs.Patches;

/// <summary>
/// Spawns the Mafia as a hard trio, TOR-style (RoleAssignmentPatch.cs:173): one spawn-chance roll;
/// requires 3+ impostor-team players; on success three impostors become Godfather, Mafia Janitor,
/// and Mafioso. Runs host-side after TOU-Mira's own role selection (Priority.VeryLow postfix) and
/// replaces three already-assigned impostor roles - players holding plain vanilla impostor roles
/// are converted first so configured custom roles are displaced only when unavoidable. The three
/// mafia roles have count 0 / hidden settings, so this is the only way they spawn.
/// </summary>
[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
public static class MafiaAssignmentPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.VeryLow)]
    public static void Postfix()
    {
        if (!AmongUsClient.Instance || !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        var chance = (int)OptionGroupSingleton<MafiaOptions>.Instance.SpawnChance;
        if (chance <= 0 || UnityEngine.Random.Range(1, 101) > chance)
        {
            return;
        }

        var impostors = Helpers.GetAlivePlayers().Where(x => x.Data?.Role != null && x.Data.Role.IsImpostor()).ToList();
        if (impostors.Count < 3)
        {
            return;
        }

        var random = new System.Random();
        var ordered = impostors
            .OrderBy(x => x.Data.Role is ICustomRole ? 1 : 0)
            .ThenBy(_ => random.Next())
            .ToList();

        ordered[0].RpcChangeRole(RoleId.Get<GodfatherRole>());
        ordered[1].RpcChangeRole(RoleId.Get<MafiaJanitorRole>());
        ordered[2].RpcChangeRole(RoleId.Get<MafiosoRole>());
    }
}
