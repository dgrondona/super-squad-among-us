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

public sealed class ApparaterMapButton : SuperSquadRoleButton<ApparaterRole>
{
    // Used only if the local player's collider is somehow unavailable when checking.
    // Fallback-only snap cap, used when the minimap texture can't be sampled (see HandleMapClick):
    // beyond this, a click's nearest reachable point is treated as "not actually on the map".
    private const float MaxSnapDistance = 1.5f;

    // Minimum minimap-texture alpha for a click to count as "on a room or hallway". The vanilla map
    // texture is fully transparent (alpha 0) over walls and off-map space and semi-transparent over
    // rooms/halls, so anything meaningfully above zero is on the ship.
    private const float RoomAlphaThreshold = 0.05f;

    // Snap cap when the alpha mask already confirmed the click is in a room: generous, so clicking the
    // middle of a large in-room obstacle (e.g. the crate pile in Storage's center) still snaps to the
    // nearest walkable floor around it instead of being rejected.
    private const float InRoomSnapDistance = 6f;

    public override string Name => TouLocale.GetParsed("SuperSquadRoleApparaterTeleport", "Teleport");
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;
    public override Color TextOutlineColor => SuperSquadColors.Apparater;
    public override float Cooldown => Math.Clamp(OptionGroupSingleton<ApparaterOptions>.Instance.TeleportCooldown + MapCooldown, 5f, 120f);

    // Re-armed every tick in FixedUpdate so it never runs out - see docs/roles/apparater.md for why
    // EffectDuration can't just be 0 on this button hierarchy.
    private const float TimerKeepAlive = 5f;

    public override float EffectDuration => TimerKeepAlive;
    public override int MaxUses => (int)OptionGroupSingleton<ApparaterOptions>.Instance.MaxUses;
    public override LoadableAsset<Sprite> Sprite => SuperSquadCrewAssets.ApparaterMapSprite;

    // Whether a teleport actually happened this activation - gates the use-refund/no-cooldown path below.
    private bool teleported;

    // The frame the map was opened, so that same click can't also register as a map-target click.
    private int openedFrame;

    // MiraAPI only drives a button's FixedUpdate while Enabled(role) is true, and dying swaps
    // Data.Role to a ghost role - so stay enabled while the map is open, or the only code that can
    // close it (FixedUpdate) would stop ticking the moment the role/kit disappears, stranding the map
    // open forever (same reason RC-XD/Dumper's own buttons do this - see their Enabled overrides).
    public override bool Enabled(RoleBehaviour? role)
    {
        return base.Enabled(role) || EffectActive;
    }

    protected override void OnClick()
    {
        teleported = false;
        openedFrame = Time.frameCount;
        BareMapVisuals.Open(Palette.Blue);
    }

    public override void OnEffectEnd()
    {
        base.OnEffectEnd();
        BareMapVisuals.Close();

        if (teleported)
        {
            return;
        }

        if (LimitedUses)
        {
            IncreaseUses();
        }

        Timer = 0f;
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        if (!EffectActive)
        {
            return;
        }

        Timer = TimerKeepAlive;
        if (Button)
        {
            Button!.cooldownTimerText.gameObject.SetActive(false);
        }

        if (!MapBehaviour.Instance || !MapBehaviour.Instance.gameObject.activeSelf)
        {
            ResetCooldownAndOrEffect();
        }
    }

    /// <summary>
    /// Handles a left-click while the teleport map is open. Called from
    /// <see cref="Patches.ApparaterMapClickPatch"/> rather than polled here - see that class for why.
    /// Click validity comes from the minimap's own texture (<see cref="MiniMapMask"/>): transparent
    /// pixels are walls/off-map, semi-transparent pixels are rooms/halls. A click confirmed in-room
    /// gets a generous snap to the nearest walkable point, so large in-room obstacles (the Storage
    /// crate pile) no longer eat the click; the old tight geometry-only snap cap survives purely as
    /// the fallback for when the texture can't be sampled.
    /// </summary>
    public void HandleMapClick()
    {
        if (!EffectActive || !MapBehaviour.Instance || !MapBehaviour.Instance.gameObject.activeSelf)
        {
            return;
        }

        if (Time.frameCount == openedFrame)
        {
            return;
        }

        // Same transient-null hazard as the Sniper's click (see docs/il2cpp-gotchas.md); the map stays
        // open, so the click simply retries next press.
        if (Camera.main == null)
        {
            Info("Apparater: click ignored - Camera.main unavailable this frame");
            return;
        }

        var clickWorldPoint = (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition);

        var snapCap = MaxSnapDistance;
        if (MiniMapMask.TryGetAlphaAtWorldPoint(clickWorldPoint, out var alpha))
        {
            if (alpha < RoomAlphaThreshold)
            {
                Info($"Apparater: click ignored - map texture alpha {alpha} says wall/off-map");
                return;
            }

            snapCap = InRoomSnapDistance;
        }
        else
        {
            Info("Apparater: map texture unavailable, falling back to geometry-only validation");
        }

        var playerControl = PlayerControl.LocalPlayer;
        var rawTarget = GetRawClickWorldPosition(clickWorldPoint);
        var origin = playerControl.GetTruePosition();
        var probeRadius = WalkableRegionSolver.GetProbeRadius(playerControl);

        // The search runs entirely inside one rendered frame, so its cost is a visible hitch if it grows.
        // Logged rather than assumed - see docs/roles/apparater.md.
        var searchTimer = System.Diagnostics.Stopwatch.StartNew();
        var reachable = WalkableRegionSolver.TryFindReachablePoint(origin, rawTarget, probeRadius, out var target);
        searchTimer.Stop();

        if (!reachable)
        {
            Info($"Apparater: no reachable point found near raw click {rawTarget} " +
                 $"(search {searchTimer.Elapsed.TotalMilliseconds:F1}ms)");
            return;
        }

        var snapDistance = Vector2.Distance(target, rawTarget);
        if (snapDistance > snapCap)
        {
            Info($"Apparater: click at {rawTarget} was {snapDistance} from the nearest reachable point {target} " +
                 $"(> {snapCap}); ignoring");
            return;
        }

        Info($"Apparater: teleporting {origin} -> {target} (raw click {rawTarget}, alpha={alpha}, " +
             $"snapDistance={snapDistance}, search {searchTimer.Elapsed.TotalMilliseconds:F1}ms)");
        teleported = true;
        playerControl.NetTransform.RpcSnapTo(target);
        ResetCooldownAndOrEffect();
    }

    // Map-sprite click -> ship-space world position.
    //
    // This uses a DIFFERENT transform from MiniMapMask's alpha sample (ColorControl.rend, on the map's
    // Background object) and that is deliberate, not a bug - the two frames are separated by a per-map
    // translation (Skeld (0.54, 1.25), Polus (-4.15, 2.45)). HerePoint's parent is the frame where
    // map-local * MapScale equals ship-world, which is why every mod parents map icons there; Background
    // is the frame the sprite's pixels live in. Each conversion targets the frame it actually needs.
    private static Vector2 GetRawClickWorldPosition(Vector2 clickWorldPoint)
    {
        var mapRoot = MapBehaviour.Instance.HerePoint.transform.parent;
        var localPoint = mapRoot.InverseTransformPoint(clickWorldPoint);
        var shipPoint = (Vector2)(localPoint * ShipStatus.Instance.MapScale);

        // Mirrored maps (Dleks) reuse Skeld's un-mirrored map sprite and flip the ship instead, so the
        // map<->ship conversion needs the sign. Prior art all converts world->map-local; ours is the
        // inverse, but a sign flip is its own inverse, so the same multiply applies here.
        shipPoint.x *= Mathf.Sign(ShipStatus.Instance.transform.localScale.x);
        return shipPoint;
    }

}
