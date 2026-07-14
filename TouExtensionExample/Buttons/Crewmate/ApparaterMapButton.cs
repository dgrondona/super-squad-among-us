using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using TouExtensionExample.Assets;
using TouExtensionExample.Options.Roles.Crewmate;
using TouExtensionExample.Roles.Crewmate;
using TownOfUs.Buttons;
using TownOfUs.Modules.Localization;
using UnityEngine;

namespace TouExtensionExample.Buttons.Crewmate;

public sealed class ApparaterMapButton : TownOfUsRoleButton<ApparaterRole>
{
    public override string Name => TouLocale.GetParsed("ExampleRoleApparaterTeleport", "Teleport");
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;
    public override Color TextOutlineColor => TouExampleColors.Apparater;
    public override float Cooldown => Math.Clamp(OptionGroupSingleton<ApparaterOptions>.Instance.TeleportCooldown + MapCooldown, 5f, 120f);
    public override float EffectDuration => OptionGroupSingleton<ApparaterOptions>.Instance.SelectTime;
    public override int MaxUses => (int)OptionGroupSingleton<ApparaterOptions>.Instance.MaxUses;
    public override LoadableAsset<Sprite> Sprite => ExampleCrewAssets.ApparaterMapSprite;

    // Guards against the same physical mouse click that pressed this ability button
    // also being read as a map click on the very next FixedUpdate tick.
    private bool justOpened;

    protected override void OnClick()
    {
        justOpened = true;
        HudManager.Instance.InitMap();
        MapBehaviour.Instance.ShowNormalMap();
    }

    public override void OnEffectEnd()
    {
        base.OnEffectEnd();
        CloseMap();
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        if (!EffectActive)
        {
            return;
        }

        if (justOpened)
        {
            justOpened = false;
            return;
        }

        if (!MapBehaviour.Instance || !MapBehaviour.Instance.gameObject.activeSelf)
        {
            // The player closed the map themselves (e.g. the in-game close button) without picking a spot.
            ResetCooldownAndOrEffect();
            return;
        }

        if (Input.GetMouseButtonDown(0) && TryGetTeleportTarget(out var target))
        {
            playerControl.NetTransform.RpcSnapTo(target);
            ResetCooldownAndOrEffect();
        }
    }

    private static void CloseMap()
    {
        if (MapBehaviour.Instance && MapBehaviour.Instance.gameObject.activeSelf)
        {
            MapBehaviour.Instance.Close();
        }
    }

    private static bool TryGetTeleportTarget(out Vector2 worldPosition)
    {
        worldPosition = default;

        // Mirrors the math TownOfUs uses to place vent/body icons on the minimap
        // (worldPos / ShipStatus.Instance.MapScale), just inverted.
        var mapRoot = MapBehaviour.Instance.HerePoint.transform.parent;
        var clickPoint = Camera.main!.ScreenToWorldPoint(Input.mousePosition);
        var localPoint = mapRoot.InverseTransformPoint(clickPoint);
        var candidate = (Vector2)(localPoint * ShipStatus.Instance.MapScale);

        if (Helpers.GetRoom(candidate) == null)
        {
            return false;
        }

        worldPosition = candidate;
        return true;
    }
}
