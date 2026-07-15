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
    // Used only if the local player's collider is somehow unavailable when checking.
    private const float FallbackProbeRadius = 0.2f;

    // Beyond this, a click's nearest reachable point is treated as "not actually on the map". See docs.
    private const float MaxSnapDistance = 1.5f;

    public override string Name => TouLocale.GetParsed("SuperSquadRoleApparaterTeleport", "Apparate");
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

        var playerControl = PlayerControl.LocalPlayer;
        var rawTarget = GetRawClickWorldPosition();
        var origin = playerControl.GetTruePosition();
        var probeRadius = GetPlayerProbeRadius();

        if (!WalkableRegionSolver.TryFindReachablePoint(origin, rawTarget, probeRadius, out var target))
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
        teleported = true;
        playerControl.NetTransform.RpcSnapTo(target);
        ResetCooldownAndOrEffect();
    }

    private static Vector2 GetRawClickWorldPosition()
    {
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
}
