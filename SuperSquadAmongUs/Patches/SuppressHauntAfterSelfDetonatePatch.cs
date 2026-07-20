using HarmonyLib;
using SuperSquadAmongUs.Modules;
using UnityEngine;

namespace SuperSquadAmongUs.Patches;

/// <summary>
/// RC-XD's Deploy/Detonate button shares the vanilla Ability keybind with the ghost Haunt menu
/// (both bound to <c>Keybinds.SecondaryAction</c>). <c>AbilityButton.DoClick()</c> polls that key
/// independently each frame; when a self-detonate kills the deployer, <c>Data.Role</c> has already
/// flipped to a ghost role by the time that poll runs, so the same key press opens Haunt right
/// after the kill. Skip the click for the one frame <see cref="RcXdCar.SelfDetonationFrame"/> marks.
/// </summary>
[HarmonyPatch(typeof(AbilityButton), nameof(AbilityButton.DoClick))]
public static class SuppressHauntAfterSelfDetonatePatch
{
    [HarmonyPrefix]
    public static bool Prefix()
    {
        return Time.frameCount != RcXdCar.SelfDetonationFrame;
    }
}
