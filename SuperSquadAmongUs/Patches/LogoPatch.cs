using HarmonyLib;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using TownOfUs.Assets;
using UnityEngine;

namespace SuperSquadAmongUs.Patches;

[HarmonyPatch]
public static class LogoPatch
{
    [HarmonyPatch(typeof(TouAssets), nameof(TouAssets.Banner), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool Prefix(ref LoadableAsset<Sprite> __result)
    {
        __result = SuperSquadAssets.Banner;
        return false;
    }
}