using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Options.Roles.Crewmate;
using SuperSquadAmongUs.Roles.Crewmate;
using TownOfUs.Buttons;
using TownOfUs.Modules.Localization;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Crewmate;

public sealed class ApparaterMapButton : TownOfUsRoleButton<ApparaterRole>
{
    private const float SearchRadiusStep = 0.2f;
    private const float SearchMaxRadius = 4f;
    private const int SearchAnglesPerRing = 16;

    // Non-trigger colliders on these layers don't represent physical obstacles
    // (matches the filter TownOfUs itself uses for placement checks, e.g.
    // SentryPlaceCameraButton/MinerPlaceVentButton).
    private const int PlayersLayer = 5;
    private const int IgnoreRaycastLayer = 8;

    public override string Name => TouLocale.GetParsed("SuperSquadRoleApparaterTeleport", "Teleport");
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;
    public override Color TextOutlineColor => SuperSquadColors.Apparater;
    public override float Cooldown => Math.Clamp(OptionGroupSingleton<ApparaterOptions>.Instance.TeleportCooldown + MapCooldown, 5f, 120f);
    public override float EffectDuration => OptionGroupSingleton<ApparaterOptions>.Instance.SelectTime;
    public override int MaxUses => (int)OptionGroupSingleton<ApparaterOptions>.Instance.MaxUses;
    public override LoadableAsset<Sprite> Sprite => SuperSquadCrewAssets.ApparaterMapSprite;

    // Edge-detected ourselves: Input.GetMouseButtonDown is a single-frame Update() flag and is
    // unreliable when only read from FixedUpdate (it can be missed entirely if no FixedUpdate
    // tick lands on the frame the click happened). Seeding this to the real state on open also
    // means the same physical click that pressed this ability button can't double as a map click
    // until the button is actually released and pressed again.
    private bool wasMouseDown;

    protected override void OnClick()
    {
        wasMouseDown = Input.GetMouseButton(0);
        HudManager.Instance.InitMap();

        var map = MapBehaviour.Instance;
        // GenericShow() is the bare "show the ship layout" call. ShowNormalMap()/ShowCountOverlay()/
        // ShowSabotageMap() additionally trigger TownOfUs's vent-icon overlay (a Harmony postfix
        // targeting those three methods specifically) and leave the task overlay visible - neither
        // of which we want for a plain teleport-target picker.
        map.GenericShow();
        map.taskOverlay.Hide();
        map.countOverlay.gameObject.SetActive(false);
        map.TrackedHerePoint.gameObject.SetActive(false);
        map.HerePoint.enabled = true;
        PlayerControl.LocalPlayer.SetPlayerMaterialColors(map.HerePoint);
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

        if (!MapBehaviour.Instance || !MapBehaviour.Instance.gameObject.activeSelf)
        {
            // The player closed the map themselves (e.g. the in-game close button) without picking a spot.
            ResetCooldownAndOrEffect();
            return;
        }

        var isMouseDown = Input.GetMouseButton(0);
        var clicked = isMouseDown && !wasMouseDown;
        wasMouseDown = isMouseDown;

        if (!clicked)
        {
            return;
        }

        var rawTarget = GetRawClickWorldPosition();
        if (TryFindNearestValidPoint(rawTarget, out var target))
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

    private static Vector2 GetRawClickWorldPosition()
    {
        // Mirrors the math TownOfUs uses to place vent/body icons on the minimap
        // (worldPos / ShipStatus.Instance.MapScale), just inverted.
        var mapRoot = MapBehaviour.Instance.HerePoint.transform.parent;
        var clickPoint = Camera.main!.ScreenToWorldPoint(Input.mousePosition);
        var localPoint = mapRoot.InverseTransformPoint(clickPoint);
        return (Vector2)(localPoint * ShipStatus.Instance.MapScale);
    }

    // Searches outward in expanding rings from the raw clicked point for the nearest spot that is
    // both inside the ship's walkable room area and not overlapping a solid obstacle (console,
    // table, engine, wall, etc.), so a click on/near furniture lands the player next to it instead
    // of inside it.
    private static bool TryFindNearestValidPoint(Vector2 candidate, out Vector2 result)
    {
        if (IsValidTeleportPoint(candidate))
        {
            result = candidate;
            return true;
        }

        for (var radius = SearchRadiusStep; radius <= SearchMaxRadius; radius += SearchRadiusStep)
        {
            for (var i = 0; i < SearchAnglesPerRing; i++)
            {
                var angle = i * (360f / SearchAnglesPerRing) * Mathf.Deg2Rad;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                var point = candidate + offset;

                if (IsValidTeleportPoint(point))
                {
                    result = point;
                    return true;
                }
            }
        }

        result = default;
        return false;
    }

    private static bool IsValidTeleportPoint(Vector2 point)
    {
        return Helpers.GetRoom(point) != null && !IsObstructed(point);
    }

    private static bool IsObstructed(Vector2 point)
    {
        var hits = Physics2D.OverlapPointAll(point, Constants.ShipAndAllObjectsMask);
        return hits.Any(c => !c.isTrigger && c.gameObject.layer != PlayersLayer && c.gameObject.layer != IgnoreRaycastLayer);
    }
}
