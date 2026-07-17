using HarmonyLib;
using MiraAPI.Hud;
using SuperSquadAmongUs.Buttons.Impostor;
using SuperSquadAmongUs.Roles.Impostor;

namespace SuperSquadAmongUs.Patches;

/// <summary>
/// Grants the Sniper full map vision - no darkness falloff, no wall shadows - for the whole aim window
/// (user request, 2026-07-16). Hooks the same method TOU-Mira's own <see cref="VisionPatch"/> uses for
/// every other role-based vision change, and reuses the exact value TOU-Mira already grants dead
/// players for "see the whole map, including through walls": <c>ShipStatus.MaxLightRadius</c>. Runs
/// with lower priority than TOU-Mira's postfix so this always has the final say while aiming; when
/// aiming ends, the condition below simply stops firing and the next call recomputes the normal value
/// (TOU-Mira's postfix runs unopposed) - no manual restore needed, unlike a one-shot field write that
/// vanilla's per-frame lighting recalculation would otherwise stomp.
/// </summary>
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CalculateLightRadius))]
public static class SniperVisionPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Low)]
    public static void Postfix(ShipStatus __instance, NetworkedPlayerInfo player, ref float __result)
    {
        if (player == null || player.IsDead || player.Object == null || player.Role is not SniperRole)
        {
            return;
        }

        if (CustomButtonSingleton<SniperSnipeButton>.Instance.EffectActive)
        {
            __result = __instance.MaxLightRadius;
        }
    }
}
