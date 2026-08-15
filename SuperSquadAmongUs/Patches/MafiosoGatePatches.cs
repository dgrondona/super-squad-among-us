using HarmonyLib;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs.Utilities;

namespace SuperSquadAmongUs.Patches;

/// <summary>
/// The Mafioso's leash (TOR): while any living Godfather exists, the Mafioso can neither kill nor
/// sabotage. Both gates are purely local UI/click logic, exactly like TOR (UpdatePatch.cs:296,
/// UsablesPatch.cs:210) - no RPCs. When the Godfather dies, both the kill and sabotage buttons
/// reappear immediately, with the kill button's cooldown NOT reset (TOR behavior).
/// </summary>
public static class MafiosoGatePatches
{
    private static bool wasGated;

    private static bool LocalMafiosoIsGated =>
        PlayerControl.LocalPlayer != null &&
        !PlayerControl.LocalPlayer.HasDied() &&
        PlayerControl.LocalPlayer.Data?.Role is MafiosoRole &&
        Helpers.GetAlivePlayers().Any(x => x.Data.Role is GodfatherRole);

    [HarmonyPatch(typeof(KillButton), nameof(KillButton.DoClick))]
    [HarmonyPrefix]
    public static bool KillClickPrefix()
    {
        return !LocalMafiosoIsGated;
    }

    [HarmonyPatch(typeof(SabotageButton), nameof(SabotageButton.DoClick))]
    [HarmonyPrefix]
    public static bool SabotageClickPrefix()
    {
        return !LocalMafiosoIsGated;
    }

    /// <summary>
    /// Mirrors TOR's per-frame kill/sabotage button state machine: both hidden every frame while
    /// gated, re-shown once on the gate lifting (vanilla only re-shows them on hud refreshes, which
    /// don't happen when the Godfather dies mid-round to e.g. a Sheriff).
    /// </summary>
    /// <param name="__instance">The hud.</param>
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    public static void HudUpdatePostfix(HudManager __instance)
    {
        if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data?.Role is not MafiosoRole)
        {
            wasGated = false;
            return;
        }

        var gated = LocalMafiosoIsGated;
        if (gated)
        {
            __instance.KillButton.Hide();
            __instance.SabotageButton.Hide();
        }
        else if (wasGated && !PlayerControl.LocalPlayer.HasDied() && !MeetingHud.Instance && !ExileController.Instance)
        {
            __instance.KillButton.Show();
            __instance.SabotageButton.Show();
        }

        wasGated = gated;
    }
}
