using HarmonyLib;
using MiraAPI.Hud;
using SuperSquadAmongUs.Buttons.Crewmate;
using UnityEngine;

namespace SuperSquadAmongUs.Patches;

/// <summary>
/// Detects the left-click that picks a teleport target for the Apparater, from within
/// <see cref="HudManager.Update"/> so it's synced to the render frame. The button's own
/// <c>FixedUpdate</c> can't do this reliably: FixedUpdate doesn't run on every rendered frame, so a
/// quick click held for less than one fixed timestep was sampled as never-pressed and dropped. Reading
/// the single-frame <see cref="Input.GetMouseButtonDown(int)"/> flag from an Update hook catches every
/// click. The button itself gates on whether its map is actually open, so this is a no-op otherwise.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class ApparaterMapClickPatch
{
    /// <summary>Forwards a frame's left-click-down to the Apparater button.</summary>
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
