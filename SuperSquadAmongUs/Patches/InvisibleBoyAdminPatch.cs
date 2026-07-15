using System.Collections.Generic;
using HarmonyLib;
using MiraAPI.Modifiers;
using SuperSquadAmongUs.Modifiers;
using UnityEngine;

namespace SuperSquadAmongUs.Patches;

/// <summary>
/// Keeps the admin table map from counting Invisible Boy while he is invisible. TOU-Mira fully replaces
/// <see cref="MapCountOverlay.Update"/> with its own <c>[HarmonyPrefix]</c> that counts players via
/// <c>Collider2D</c> overlap against each room, so - short of editing TOU-Mira source - the only way to
/// exclude him from that count is to disable his collider for the duration of that overlap check. A
/// living player's collider cannot simply stay disabled (he would clip through walls), so it is toggled
/// off immediately before the overlap check runs and restored immediately after.
/// </summary>
[HarmonyPatch(typeof(MapCountOverlay), nameof(MapCountOverlay.Update))]
public static class InvisibleBoyAdminPatch
{
    private static readonly List<Collider2D> DisabledColliders = new();

    /// <summary>
    /// Disables the collider of every currently-invisible player. Runs at <see cref="Priority.High"/> so
    /// it fires before TOU-Mira's own counting prefix runs the overlap check; it is a <see langword="void"/>
    /// prefix that never returns <see langword="false"/> itself, so the rest of the prefix chain (and the
    /// original method, since TOU-Mira's own prefix skips it) still runs exactly as before.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPriority(Priority.High)]
    public static void Prefix()
    {
        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (!player || !player.HasModifier<InvisibleBoyModifier>() || !player.Collider || !player.Collider.enabled)
            {
                continue;
            }

            player.Collider.enabled = false;
            DisabledColliders.Add(player.Collider);
        }
    }

    /// <summary>
    /// Re-enables exactly the colliders this patch disabled - never anything else, since ghost roles can
    /// legitimately keep their own colliders off. Postfixes still run even when a prefix earlier in the
    /// chain (such as TOU-Mira's own) returns <see langword="false"/> and skips the original method, so
    /// this always fires to balance the prefix. Physics only advances on <c>FixedUpdate</c>, so a
    /// collider toggled off and back on within a single <c>Update</c> can never affect movement.
    /// </summary>
    [HarmonyPostfix]
    public static void Postfix()
    {
        // The null filter guards against a player (and their collider) despawning mid-frame.
        foreach (var collider in DisabledColliders.Where(collider => collider))
        {
            collider.enabled = true;
        }

        DisabledColliders.Clear();
    }
}
