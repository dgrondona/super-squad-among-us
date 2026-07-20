using HarmonyLib;

namespace SuperSquadAmongUs.Patches;

/// <summary>
/// Skips the vanilla kill-stinger overlay when killer == victim - a pairing only modded self-murders
/// produce, and vanilla <c>ShowKillAnimation</c> throws an IL2CPP <c>MethodAccessException</c> for it,
/// which poisons il2cpp state a later native crash trips over. See "Local-death native crash" in
/// docs/il2cpp-gotchas.md.
/// </summary>
[HarmonyPatch(typeof(KillOverlay), nameof(KillOverlay.ShowKillAnimation),
    typeof(NetworkedPlayerInfo), typeof(NetworkedPlayerInfo))]
public static class SelfKillOverlayPatch
{
    [HarmonyPrefix]
    public static bool Prefix(NetworkedPlayerInfo killer, NetworkedPlayerInfo victim)
    {
        return killer == null || victim == null || killer.PlayerId != victim.PlayerId;
    }
}
