using TownOfUs.Patches.Misc;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// Opens the ship map as a plain target-picker - no task/vent icons, the local player's own position
/// marker, and a caller-chosen background tint - shared by any ability that needs it (currently
/// Apparater's teleport; a future impostor map ability, e.g. a click-to-airstrike, would call this too,
/// passing <see cref="Palette.ImpostorRed"/> instead of <see cref="Palette.Blue"/>).
/// </summary>
internal static class BareMapVisuals
{
    /// <summary>
    /// Shows the bare map (ship layout only, no tasks/vents/counts) with the local player's live
    /// position marked in their own color and the background pulsing in <paramref name="backgroundColor"/>.
    /// </summary>
    /// <param name="backgroundColor">
    /// The map's background pulse color - <see cref="Palette.Blue"/> for a crewmate-facing map (the same
    /// blue the vanilla task map uses, per TOU-Mira's own <c>MapBehaviourPatch.ShowNormalMap</c> postfix),
    /// or <see cref="Palette.ImpostorRed"/> for a future impostor-facing one.
    /// </param>
    public static void Open(Color backgroundColor)
    {
        HudManager.Instance.InitMap();

        var map = MapBehaviour.Instance;
        // GenericShow() is the bare "show the ship layout" call. ShowNormalMap()/ShowCountOverlay()/
        // ShowSabotageMap() additionally trigger TownOfUs's vent-icon overlay (a Harmony postfix
        // targeting those three methods specifically) and leave the task overlay visible - neither of
        // which we want. Unlike them, GenericShow() also doesn't set ColorControl or the here-marker
        // color, so both are set explicitly below rather than relying on a Show* side effect.
        map.GenericShow();
        map.taskOverlay.Hide();
        map.countOverlay.gameObject.SetActive(false);
        map.TrackedHerePoint.gameObject.SetActive(false);

        // Marker = the local player's own cosmetic color (cyan for a cyan player, etc.). This must go
        // through SetPlayerMaterialColors - the here-marker uses the player-cosmetic material pipeline,
        // and a plain `HerePoint.color = ...` assignment bypasses it and renders the wrong color.
        map.HerePoint.enabled = true;
        PlayerControl.LocalPlayer.SetPlayerMaterialColors(map.HerePoint);

        // Background pulse. ColorControl is an AlphaPulse; SetColor is how the vanilla normal map tints
        // its background too (crew blue / impostor red).
        map.ColorControl.SetColor(backgroundColor);

        ClearPersistentMapIcons();
    }

    public static void Close()
    {
        if (MapBehaviour.Instance && MapBehaviour.Instance.gameObject.activeSelf)
        {
            MapBehaviour.Instance.Close();
        }
    }

    /// <summary>
    /// Destroys any leftover vent/dead-body map icons that TownOfUs's <see cref="ShowVentsPatch"/> may
    /// have created on a previous <c>ShowNormalMap</c>/<c>ShowSabotageMap</c> open. Those icons are
    /// GameObjects parented under the map root and are only torn down at round start (or when the vent
    /// toggle is off) - never when the map closes - so without this they'd still be sitting under the
    /// map root and reappear the moment we <c>GenericShow()</c> it, even though GenericShow itself never
    /// creates them. Mirrors ShowVentsPatch's own teardown; the dicts self-heal (the vanilla task map
    /// recreates its icons on its next open).
    /// </summary>
    private static void ClearPersistentMapIcons()
    {
        foreach (var icon in ShowVentsPatch.VentIcons.Values.Where(icon => icon))
        {
            Object.Destroy(icon);
        }

        foreach (var icon in ShowVentsPatch.BodyIcons.Values.Where(icon => icon))
        {
            Object.Destroy(icon);
        }

        ShowVentsPatch.VentIcons.Clear();
        ShowVentsPatch.BodyIcons.Clear();
        ShowVentsPatch.VentNetworks.Clear();
    }
}
