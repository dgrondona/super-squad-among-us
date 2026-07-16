using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modules;
using SuperSquadAmongUs.Options.Roles.Impostor;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs;
using TownOfUs.Buttons;
using TownOfUs.Modules.Localization;
using TownOfUs.Networking;
using TownOfUs.Utilities;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SuperSquadAmongUs.Buttons.Impostor;

/// <summary>
/// The Sniper's shot (user design): press the button to shoulder the rifle, then click anywhere in the
/// world within the aim window. A piercing bullet flies from the sniper's body through the clicked
/// point, through walls, killing everyone on the line. While aiming, a guide sprite points from the
/// sniper toward the cursor.
/// </summary>
public sealed class SniperSnipeButton : TownOfUsRoleButton<SniperRole>
{
    private GameObject? aimGuide;

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
        // Arming is local-only; nothing to sync until the trigger is pulled.
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        if (!EffectActive || playerControl.HasDied() || MeetingHud.Instance)
        {
            ClearAimGuide();
            if (EffectActive && (playerControl.HasDied() || MeetingHud.Instance))
            {
                EffectActive = false;
                SetTimer(Cooldown);
            }

            return;
        }

        UpdateAimGuide(playerControl);

        // Fire on left click, ignoring clicks on UI (HUD buttons, open map, chat).
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUi() &&
            !(MapBehaviour.Instance && MapBehaviour.Instance.IsOpen))
        {
            Fire(playerControl);
        }
    }

    public override void OnEffectEnd()
    {
        ClearAimGuide();
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
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
        ClearAimGuide();

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

    private void UpdateAimGuide(PlayerControl sniper)
    {
        if (aimGuide == null)
        {
            aimGuide = new GameObject("SuperSquadSniperAimGuide");
            var renderer = aimGuide.AddComponent<SpriteRenderer>();
            renderer.sprite = SuperSquadImpAssets.SniperGuideSprite.LoadAsset();
        }

        var origin = (Vector2)sniper.GetTruePosition();
        var mouseWorld = (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var direction = (mouseWorld - origin).normalized;
        var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        var position = origin + (direction * 0.6f);
        aimGuide.transform.position = new Vector3(position.x, position.y, position.y / 1000f - 1f);
        aimGuide.transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void ClearAimGuide()
    {
        if (aimGuide != null)
        {
            UnityEngine.Object.Destroy(aimGuide);
            aimGuide = null;
        }
    }
}
