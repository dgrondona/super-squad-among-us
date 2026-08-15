using HarmonyLib;
using MiraAPI.Hud;
using SuperSquadAmongUs.Buttons.Crewmate;
using UnityEngine;

namespace SuperSquadAmongUs.Patches;

/// <summary>
/// Detects the Apparater's map-target click from <see cref="HudManager.Update"/> instead of the
/// button's own FixedUpdate, which doesn't run on every rendered frame and can drop quick clicks. See
/// docs/roles/apparater.md. Runs at <see cref="Priority.First"/> and is exception-guarded for the same
/// reason as <see cref="SniperAimPatch"/>: when any earlier postfix on this shared method throws,
/// Harmony skips the remaining postfixes for that frame, and TOU-Mira hangs several patches off
/// HudManager.Update.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class ApparaterMapClickPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    public static void Postfix()
    {
        try
        {
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            CustomButtonSingleton<ApparaterMapButton>.Instance.HandleMapClick();
        }
        catch (Exception e)
        {
            Error($"Apparater: HandleMapClick threw: {e}");
        }
    }
}
