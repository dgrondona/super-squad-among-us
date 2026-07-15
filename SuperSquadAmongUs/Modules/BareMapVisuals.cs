using TownOfUs.Patches.Misc;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// Opens the ship map as a plain target-picker: no task/vent icons, the local player's own position
/// marker, and a caller-chosen background tint. Shared by any ability that needs this - currently
/// Apparater; see docs/roles/apparater.md for a possible future impostor consumer.
/// </summary>
internal static class BareMapVisuals
{
    public static void Open(Color backgroundColor)
    {
        HudManager.Instance.InitMap();

        var map = MapBehaviour.Instance;
        // GenericShow() skips the vent-icon overlay and task overlay ShowNormalMap()/ShowCountOverlay()/
        // ShowSabotageMap() trigger, but also skips their color/marker side effects, so those are set
        // explicitly below.
        map.GenericShow();
        map.taskOverlay.Hide();
        map.countOverlay.gameObject.SetActive(false);
        map.TrackedHerePoint.gameObject.SetActive(false);

        // Must go through SetPlayerMaterialColors - a plain `.color =` bypasses the player-cosmetic
        // material pipeline the here-marker renders through and shows the wrong color.
        map.HerePoint.enabled = true;
        PlayerControl.LocalPlayer.SetPlayerMaterialColors(map.HerePoint);

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

    // ShowVentsPatch's vent/body icons are only torn down at round start, never on map close, so a
    // prior ShowNormalMap/ShowSabotageMap open elsewhere (e.g. the vanilla task map) would otherwise
    // leave them visible here too.
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
