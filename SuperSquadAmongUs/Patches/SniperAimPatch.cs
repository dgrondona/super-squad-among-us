using HarmonyLib;
using MiraAPI.Hud;
using SuperSquadAmongUs.Buttons.Impostor;

namespace SuperSquadAmongUs.Patches;

/// <summary>
/// Drives the Sniper's aim guide and click-to-fire from <see cref="HudManager.Update"/> instead of the
/// button's own FixedUpdate, which doesn't run on every rendered frame and drops quick clicks (same
/// pitfall as <see cref="ApparaterMapClickPatch"/>). <see cref="SniperSnipeButton.HandleAimFrame"/>
/// self-guards and is a no-op unless the local player has an active aim window.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class SniperAimPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        CustomButtonSingleton<SniperSnipeButton>.Instance.HandleAimFrame();
    }
}
