using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modules;
using SuperSquadAmongUs.Options.Roles.Crewmate;
using SuperSquadAmongUs.Roles.Crewmate;
using TownOfUs.Buttons;
using TownOfUs.Modules.Localization;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Crewmate;

public sealed class ApparaterMapButton : TownOfUsRoleButton<ApparaterRole>
{
    // Fallback probe radius if the local player's collider is ever unavailable when checking
    // (shouldn't normally happen - the player always has a collider while alive).
    private const float FallbackProbeRadius = 0.2f;

    // A click doesn't land exactly where the reachability search's nearest-valid-point does: for a
    // click that's basically on open floor (or barely clipping a wall/obstacle edge), that gap is a
    // small precision nudge. For a click on a wall, or entirely outside the ship, the nearest reachable
    // point can be far away (however far it is back to actual floor). Treating "far" as "the click
    // wasn't actually on the map" gives us "clicking outside the play area does nothing" without
    // needing to hit-test the map's artwork directly.
    private const float MaxSnapDistance = 1.5f;

    private static bool loggedMasksOnce;

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

        LogMasksOnce();
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
        var origin = playerControl.GetTruePosition();
        var probeRadius = GetPlayerProbeRadius();

        LogClickDiagnostics(origin, rawTarget, probeRadius);

        if (!WalkableRegionSolver.TryFindReachablePoint(origin, rawTarget, probeRadius, playerControl.Collider, out var target))
        {
            Info($"Apparater: no reachable point found near raw click {rawTarget}");
            return;
        }

        var snapDistance = Vector2.Distance(target, rawTarget);
        if (snapDistance > MaxSnapDistance)
        {
            Info($"Apparater: click at {rawTarget} was {snapDistance} from the nearest reachable point {target} " +
                 $"(> {MaxSnapDistance}); treating as outside the play area and ignoring");
            return;
        }

        Info($"Apparater: teleporting {origin} -> {target} (raw click {rawTarget}, snapDistance={snapDistance})");
        playerControl.NetTransform.RpcSnapTo(target);
        ResetCooldownAndOrEffect();
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

    private static float GetPlayerProbeRadius()
    {
        var collider = PlayerControl.LocalPlayer.Collider;
        return collider ? Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y) : FallbackProbeRadius;
    }

    // Logs the resolved integer value of each Constants.*Mask once per session - these are IL2CPP
    // native fields with no statically-recoverable value (confirmed by direct inspection of the
    // compiled game assembly), so the only way to actually know what they contain is to read them
    // back at runtime like this.
    private static void LogMasksOnce()
    {
        if (loggedMasksOnce)
        {
            return;
        }

        loggedMasksOnce = true;
        Info($"Apparater: Constants.ShipOnlyMask={Constants.ShipOnlyMask} ShipAndObjectsMask={Constants.ShipAndObjectsMask} " +
             $"ShipAndAllObjectsMask={Constants.ShipAndAllObjectsMask} NotShipMask={Constants.NotShipMask} " +
             $"PlayersOnlyMask={Constants.PlayersOnlyMask}");
    }

    // Dumps every collider actually present at the raw clicked point (unmasked - all layers, both
    // trigger and solid), so a single test click still tells us definitively what's physically there
    // if the reachability search ever fails to block a wall/obstacle again.
    private static void LogClickDiagnostics(Vector2 origin, Vector2 point, float radius)
    {
        var hits = Physics2D.OverlapCircleAll(point, radius);
        Info($"Apparater: click at {point} (origin {origin}), probeRadius={radius}, {hits.Length} collider(s) present at click (unmasked):");
        foreach (var hit in hits)
        {
            Info($"Apparater:   '{hit.gameObject.name}' layer={hit.gameObject.layer} ({LayerMask.LayerToName(hit.gameObject.layer)}) isTrigger={hit.isTrigger} tag={hit.tag}");
        }

        var blockedBetween = PhysicsHelpers.AnythingBetween(PlayerControl.LocalPlayer.Collider, origin, point, Constants.ShipAndAllObjectsMask, false);
        Info($"Apparater:   AnythingBetween(origin, click)={blockedBetween}");
    }
}
