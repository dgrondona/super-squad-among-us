using HarmonyLib;

namespace SuperSquadAmongUs.Patches;

/// <summary>
/// Skips the vanilla kill-stinger overlay when a player is credited with their own death
/// (killer == victim). Vanilla gameplay never produces that pairing - only modded self-murders do
/// (RC-XD blast, Astral fade, TOU Sheriff misfire, TOU Bomber self-bomb) - and vanilla
/// <c>ShowKillAnimation</c> is broken for it: it throws an IL2CPP MethodAccessException
/// (enumerates a <c>DeadBody[]</c> where it expects the kill-animation array), aborting il2cpp
/// generic-class initialization mid-way. That poisoned state is the prime suspect for the native
/// crash in the post-death asset-unload scan - see the "Local-death native crash" entry in
/// docs/il2cpp-gotchas.md.
/// </summary>
[HarmonyPatch(typeof(KillOverlay), nameof(KillOverlay.ShowKillAnimation),
    typeof(NetworkedPlayerInfo), typeof(NetworkedPlayerInfo))]
public static class SelfKillOverlayPatch
{
    /// <summary>Skips the overlay for self-kills; lets every normal kill through.</summary>
    /// <param name="killer">The credited killer.</param>
    /// <param name="victim">The dead player.</param>
    /// <returns>False to skip the vanilla overlay when killer == victim.</returns>
    [HarmonyPrefix]
    public static bool Prefix(NetworkedPlayerInfo killer, NetworkedPlayerInfo victim)
    {
        return killer == null || victim == null || killer.PlayerId != victim.PlayerId;
    }
}
