using System;
using HarmonyLib;
using SuperSquadAmongUs.Roles.Crewmate;

namespace SuperSquadAmongUs.Patches;

/// <summary>
/// Ticks each Invisible Boy's "can anyone see me right now?" evaluation once per physics frame for
/// EVERY player on EVERY client - including players this client doesn't own (dummies, remote players) -
/// by hooking <see cref="PlayerControl.FixedUpdate"/>. The passive has to be computed locally by each
/// observer (not just on the Invisible Boy's own client) so the invisibility is consistent for everyone
/// and works against dummies; the role's own tick only reliably runs for the local player. See
/// docs/roles/invisible-boy.md.
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
public static class InvisibleBoyVisibilityPatch
{
    /// <summary>Forwards each player's frame to their Invisible Boy visibility evaluation, if they are one.</summary>
    [HarmonyPostfix]
    public static void Postfix(PlayerControl __instance)
    {
        if (!__instance || __instance.Data?.Role is not InvisibleBoyRole role)
        {
            return;
        }

        // This runs inside the vanilla PlayerControl.FixedUpdate postfix chain for EVERY player; an
        // unhandled throw here would break that chain for everyone on this client. Contain it.
        try
        {
            role.UpdateVisibilityState(__instance);
        }
        catch (Exception e)
        {
            Error($"InvisibleBoyVisibilityPatch failed for player {__instance.PlayerId}: {e}");
        }
    }
}
