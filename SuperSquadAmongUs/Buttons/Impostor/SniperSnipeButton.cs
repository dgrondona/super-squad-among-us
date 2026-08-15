using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Modifiers;
using MiraAPI.Networking;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modules;
using SuperSquadAmongUs.Options.Roles.Impostor;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs;
using TownOfUs.Buttons;
using TownOfUs.Modifiers;
using TownOfUs.Modifiers.Neutral;
using TownOfUs.Modules.Localization;
using TownOfUs.Networking;
using TownOfUs.Utilities;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SuperSquadAmongUs.Buttons.Impostor;

/// <summary>
/// Press to aim; a click anywhere in the aim window fires a piercing shot from the sniper's body
/// through the click point, through walls and players. While aiming, the sniper is frozen in place and
/// sees through walls (shadows disabled). No projectile/aim guide is rendered - the visual machinery
/// survives in <see cref="SniperShots"/> for future use. Aiming and firing run per rendered frame via
/// <see cref="Patches.SniperAimPatch"/>, not this button's FixedUpdate (fixed tick drops clicks - see
/// docs/il2cpp-gotchas.md).
/// </summary>
public sealed class SniperSnipeButton : SuperSquadRoleButton<SniperRole>
{
    // The frame the aim window was armed, so the button-press click can't also fire the shot
    // (HUD buttons are collider-based PassiveButtons, invisible to EventSystem UI checks).
    private int armedFrame;

    // Whether the freeze + wall-vision aim state is currently applied, so EndAim is idempotent
    // (it can be reached from Fire, OnEffectEnd, and the FixedUpdate cancel path).
    private bool aimLockActive;

    public override string Name => TouLocale.GetParsed("SuperSquadRoleSniperSnipe", "Snipe");
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => TownOfUsColors.Impostor;
    public override float Cooldown => Math.Clamp(OptionGroupSingleton<SniperOptions>.Instance.SnipeCooldown + MapCooldown, 5f, 120f);
    public override float EffectDuration => OptionGroupSingleton<SniperOptions>.Instance.AimWindow;
    public override LoadableAsset<Sprite> Sprite => SuperSquadImpAssets.SniperSnipeSprite;

    // MiraAPI only drives a button's FixedUpdate while Enabled(role) is true, and dying swaps
    // Data.Role to a ghost role - stay enabled while the aim state is pending so the death-cancel
    // path can still run EndAim (docs/il2cpp-gotchas.md).
    public override bool Enabled(RoleBehaviour? role)
    {
        return base.Enabled(role) || EffectActive || aimLockActive;
    }

    public override bool CanUse()
    {
        if (HudManager.Instance.Chat.IsOpenOrOpening || MeetingHud.Instance)
        {
            return false;
        }

        return base.CanUse() && !EffectActive;
    }

    protected override void OnClick()
    {
        armedFrame = Time.frameCount;
        BeginAim();
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        // Only aim-window cancellation lives here; clicks are handled per-frame in HandleAimFrame.
        if (EffectActive && (playerControl.HasDied() || MeetingHud.Instance))
        {
            EffectActive = false;
            SetTimer(Cooldown);
            EndAim();
        }
    }

    public override void OnEffectEnd()
    {
        // Natural aim-window timeout without a shot. Fire()'s own path already ran EndAim by the
        // time the framework gets here - EndAim self-guards, so calling it again is a no-op.
        EndAim();
    }

    /// <summary>
    /// Fires on left click. Called every rendered frame from <see cref="Patches.SniperAimPatch"/>
    /// while the aim window is active.
    /// </summary>
    public void HandleAimFrame()
    {
        var sniper = PlayerControl.LocalPlayer;
        if (!EffectActive || sniper == null || sniper.HasDied() || MeetingHud.Instance)
        {
            return;
        }

        // Self-heal the aim state every rendered frame (house doctrine, see docs/il2cpp-gotchas.md):
        // vanilla animations flip moveable back on, and other patches (zoom, death handling) touch
        // ShadowQuad on their own schedule.
        if (aimLockActive)
        {
            sniper.moveable = false;
            if (HudManager.Instance.ShadowQuad.gameObject.activeSelf)
            {
                HudManager.Instance.ShadowQuad.gameObject.SetActive(false);
            }
        }

        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        // A click happened during an active aim window - from here on every rejection is logged, so
        // a playtest "clicking did nothing" report can be diagnosed from the BepInEx log
        // (BepInEx/LogOutput.log) instead of guessed at.
        if (Time.frameCount == armedFrame)
        {
            Info("Sniper: click ignored - same frame the aim window was armed");
            return;
        }

        if (HudManager.Instance.Chat.IsOpenOrOpening)
        {
            Info("Sniper: click ignored - chat is open");
            return;
        }

        if (MapBehaviour.Instance && MapBehaviour.Instance.IsOpen)
        {
            Info("Sniper: click ignored - map is open");
            return;
        }

        if (IsClickOnHud())
        {
            return;
        }

        // Same gating the TOU base ClickHandler applies to the arming click - being hacked or
        // disabled mid-window must also block the trigger pull. Must respect each DisabledModifier's
        // own CanUseAbilities opt-out (e.g. GrenadierFlashModifier, EclipsalBlindModifier both set it
        // true), same as TownOfUsButton.CanUse() and every other button's fire/click gate in this repo
        // (see RcXdDeployButton, DetonatorAttachButton) - a bare HasModifier<DisabledModifier>() presence
        // check would block on modifiers that explicitly opt out of blocking abilities.
        if (sniper.HasModifier<GlitchHackedModifier>() ||
            sniper.GetModifiers<DisabledModifier>().Any(x => !x.CanUseAbilities))
        {
            Info("Sniper: click ignored - sniper is hacked or disabled");
            return;
        }

        // Camera.main does a tag lookup every call and can be transiently null (TOU-Mira guards the
        // same call in its own click handlers). Bailing leaves EffectActive true, so the aim window
        // stays open and the next click simply retries.
        if (Camera.main == null)
        {
            Info("Sniper: click ignored - Camera.main unavailable this frame");
            return;
        }

        Fire(sniper);
    }

    // Whether the current click landed on a HUD control rather than the game world. Among Us HUD
    // buttons are collider-based PassiveButtons on the UI layer, so probe that layer through the UI
    // camera; the EventSystem check covers uGUI overlays like chat. Only colliders that actually
    // belong to a PassiveButton block the shot: HudManager is parented to the camera, so UI-layer
    // objects physically overlap the play area in world space, and non-interactive things live there
    // too (e.g. TOU's tracking arrows are created on layer 5) - blocking on ANY UI-layer collider
    // silently ate legitimate aim clicks.
    private static bool IsClickOnHud()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            Info("Sniper: click ignored - EventSystem reports pointer over uGUI");
            return true;
        }

        var uiCamera = HudManager.Instance.UICamera;
        if (uiCamera == null)
        {
            return false;
        }

        var uiPoint = (Vector2)uiCamera.ScreenToWorldPoint(Input.mousePosition);
        var hit = Physics2D.OverlapPoint(uiPoint, LayerMask.GetMask("UI"));
        if (hit == null)
        {
            return false;
        }

        if (hit.GetComponentInParent<PassiveButton>() != null)
        {
            Info($"Sniper: click ignored - on HUD control '{hit.name}'");
            return true;
        }

        Info($"Sniper: click allowed - UI-layer collider '{hit.name}' is not a clickable control");
        return false;
    }

    private void Fire(PlayerControl sniper)
    {
        var options = OptionGroupSingleton<SniperOptions>.Instance;
        var origin = SniperShots.GetShotOrigin(sniper);
        var clickPoint = (Vector2)Camera.main!.ScreenToWorldPoint(Input.mousePosition);
        var direction = (clickPoint - origin).normalized;

        if (direction == Vector2.zero)
        {
            Info("Sniper: click ignored - click landed exactly on the shot origin");
            return;
        }

        // End the aim window and start the cooldown regardless of whether anything was hit.
        EffectActive = false;
        SetTimer(Cooldown);
        EndAim();

        var victims = SniperShots.FindHits(sniper, origin, direction, options.CanKillImpostors);
        Info($"Sniper: fired from {origin} toward {clickPoint}; {victims.Count} victim(s)");
        if (victims.Count > 0)
        {
            // Explicit OutsideMeeting (not the List<PlayerControl> overload's implicit
            // MeetingCheck.Ignore default) - a remote client already on the meeting screen from a
            // report/emergency RPC race must not still apply this kill.
            sniper.RpcSpecialMultiMurder(victims, MeetingCheck.OutsideMeeting, true, teleportMurderer: false,
                playKillSound: true, causeOfDeath: "SuperSquadSniper");
        }
    }

    // Root the sniper in place (same freeze DevouredModifier uses, minus the network pause sync
    // concerns - this is the local player) and drop the wall shadows so they can line up shots
    // through walls (HudManager.ShadowQuad, TOU-Mira's MedSpirit/Spectator/Teleporter pattern; the
    // shadow quad is the sole wall-occlusion mechanism - light radius only sizes the darkness circle).
    private void BeginAim()
    {
        var sniper = PlayerControl.LocalPlayer;
        sniper.moveable = false;
        sniper.MyPhysics.ResetMoveState();
        sniper.NetTransform.SetPaused(true);
        HudManager.Instance.ShadowQuad.gameObject.SetActive(false);
        aimLockActive = true;
    }

    // Idempotent counterpart to BeginAim; reachable from Fire, timeout, and death/meeting cancel.
    private void EndAim()
    {
        if (!aimLockActive)
        {
            return;
        }

        aimLockActive = false;
        var sniper = PlayerControl.LocalPlayer;
        sniper.moveable = true;
        sniper.NetTransform.SetPaused(false);

        // Vanilla's own restore rule (HudManagerPatches.cs:99 in TOU-Mira): shadows on for the
        // living, off for the dead.
        HudManager.Instance.ShadowQuad.gameObject.SetActive(!sniper.Data.IsDead);
    }
}
