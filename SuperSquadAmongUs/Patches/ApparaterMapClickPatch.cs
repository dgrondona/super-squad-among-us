using HarmonyLib;
using MiraAPI.Hud;
using SuperSquadAmongUs.Buttons.Crewmate;
using UnityEngine;

namespace SuperSquadAmongUs.Patches;

/// <summary>
/// Detects the Apparater's map-target click from <see cref="HudManager.Update"/> instead of the
/// button's own FixedUpdate, which doesn't run on every rendered frame and can drop quick clicks. See
/// docs/roles/apparater.md.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class ApparaterMapClickPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        CustomButtonSingleton<ApparaterMapButton>.Instance.HandleMapClick();
    }
}
