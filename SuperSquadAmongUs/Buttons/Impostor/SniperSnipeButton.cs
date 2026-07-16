using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Modifiers;
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
/// The Sniper's shot (user design): press the button to shoulder the rifle, then click anywhere in the
/// world within the aim window. A piercing bullet flies from the sniper's body through the clicked
/// point, through walls, killing everyone on the line. There is no aim assist while deciding where to
/// click - the projectile visual only appears at the moment of firing (see <see cref="Fire"/> and
/// <see cref="SniperShots.ShowShotLocally"/>). Aiming and firing run per rendered frame via
/// <see cref="Patches.SniperAimPatch"/> - the button's own FixedUpdate runs on the fixed tick and
/// drops mouse clicks (same pitfall as the Apparater's map click, see docs/roles/apparater.md).
/// </summary>
public sealed class SniperSnipeButton : TownOfUsRoleButton<SniperRole>
{
    // The frame the aim window was armed, so the button-press click can't also fire the shot
    // (HUD buttons are collider-based PassiveButtons, invisible to EventSystem UI checks).
    private int armedFrame;

    public override string Name => TouLocale.GetParsed("SuperSquadRoleSniperSnipe", "Snipe");
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => TownOfUsColors.Impostor;
    public override float Cooldown => Math.Clamp(OptionGroupSingleton<SniperOptions>.Instance.SnipeCooldown + MapCooldown, 5f, 120f);
    public override float EffectDuration => OptionGroupSingleton<SniperOptions>.Instance.AimWindow;
    public override LoadableAsset<Sprite> Sprite => SuperSquadImpAssets.SniperSnipeSprite;

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
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        // Only aim-window cancellation lives here; clicks are handled per-frame in HandleAimFrame.
        if (EffectActive && (playerControl.HasDied() || MeetingHud.Instance))
        {
            EffectActive = false;
            SetTimer(Cooldown);
        }
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

        if (Time.frameCount == armedFrame || !Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (IsClickOnHud() || (MapBehaviour.Instance && MapBehaviour.Instance.IsOpen))
        {
            return;
        }

        // Same gating the TOU base ClickHandler applies to the arming click - being hacked or
        // disabled mid-window must also block the trigger pull.
        if (sniper.HasModifier<GlitchHackedModifier>() || sniper.HasModifier<DisabledModifier>())
        {
            return;
        }

        Fire(sniper);
    }

    // Whether the current click landed on a HUD element rather than the game world. Among Us HUD
    // buttons are collider-based PassiveButtons on the UI layer, so probe that layer through the UI
    // camera; the EventSystem check still covers uGUI overlays like chat.
    private static bool IsClickOnHud()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }

        var uiPoint = (Vector2)HudManager.Instance.UICamera.ScreenToWorldPoint(Input.mousePosition);
        return Physics2D.OverlapPoint(uiPoint, LayerMask.GetMask("UI"));
    }

    private void Fire(PlayerControl sniper)
    {
        var options = OptionGroupSingleton<SniperOptions>.Instance;
        var origin = (Vector2)sniper.GetTruePosition();
        var clickPoint = (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var direction = (clickPoint - origin).normalized;

        if (direction == Vector2.zero)
        {
            return;
        }

        // End the aim window and start the cooldown regardless of whether anything was hit.
        EffectActive = false;
        SetTimer(Cooldown);

        if (options.BulletVisibleToOthers)
        {
            SniperShots.RpcShowShot(sniper, origin.x, origin.y, direction.x, direction.y);
        }
        else
        {
            SniperShots.ShowShotLocally(origin, direction);
        }

        var victims = SniperShots.FindHits(sniper, origin, direction, options.CanKillImpostors);
        if (victims.Count > 0)
        {
            sniper.RpcSpecialMultiMurder(victims, true, teleportMurderer: false, playKillSound: true,
                causeOfDeath: "SuperSquadSniper");
        }
    }

}
