using MiraAPI.Keybinds;
using MiraAPI.Modifiers;
using MiraAPI.Networking;
using MiraAPI.PluginLoading;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Modules;
using TownOfUs.Assets;
using TownOfUs.Buttons;
using TownOfUs.Modifiers;
using TownOfUs.Modifiers.Neutral;
using TownOfUs.Modules.Localization;
using TownOfUs.Networking;
using TownOfUs.Utilities;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SuperSquadAmongUs.Buttons;

/// <summary>
/// Shared bases for abilities Gooper/Kirby unlock at runtime via <see cref="GrantableAbility"/> flags
/// (see docs/roles/gooper.md / docs/roles/kirby.md). Growing the pool costs one shared base here plus
/// one thin sealed subclass per (role, ability) pair supplying only art/color/cooldown - not a full
/// reimplementation each time. All real logic (targeting, click handling, RPC calls) lives here,
/// mirrored from the role this ability was ported from (Sentinel's kill button, the Sniper, the
/// Swooper) but gated by the holder role's unlocked-ability flag instead of a fixed role type.
/// </summary>
[MiraIgnore]
public abstract class GrantedKillButtonBase<TRole> : TownOfUsKillRoleButton<TRole, PlayerControl>, IKillButton
    where TRole : RoleBehaviour, IAbilityGrantHolder
{
    public override string Name => TranslationController.Instance.GetStringWithDefault(StringNames.KillLabel, "Kill");
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;

    // Standard kill-button art so a granted kill reads as a kill, not a placeholder ability. A subclass
    // whose role already claims PrimaryAction for its base ability (Kirby's Swallow) overrides Keybind.
    public override LoadableAsset<Sprite> Sprite => TouAssets.KillSprite;

    /// <inheritdoc />
    /// <remarks>Hidden/disabled until the holder role has unlocked <see cref="GrantableAbility.Kill"/>.</remarks>
    public override bool Enabled(RoleBehaviour? role)
    {
        return base.Enabled(role) && Role.UnlockedAbilities.HasFlag(GrantableAbility.Kill);
    }

    public override PlayerControl? GetTarget()
    {
        return PlayerControl.LocalPlayer.GetClosestLivingPlayer(true, Distance);
    }

    protected override void OnClick()
    {
        if (Target == null)
        {
            Error($"{GetType().Name}: Target is null");
            return;
        }

        PlayerControl.LocalPlayer.RpcCustomMurder(Target);
    }
}

/// <summary>
/// Toggles a self-only concealment modifier, gated by <see cref="GrantableAbility.Swoop"/>. The
/// concrete modifier type is supplied by <typeparamref name="TModifier"/> rather than reusing
/// TOU-Mira's own <c>SwoopModifier</c> directly, since that class hard-codes calls into the Swooper's
/// own button singleton (see <see cref="Modifiers.GrantedSwoopModifierBase"/> for why).
/// </summary>
[MiraIgnore]
public abstract class GrantedSwoopButtonBase<TRole, TModifier> : TownOfUsRoleButton<TRole>
    where TRole : RoleBehaviour, IAbilityGrantHolder
    where TModifier : BaseModifier
{
    public override bool ZeroIsInfinite { get; set; } = true;

    // ModifierAction keybind: the last free distinct slot for a role that may hold Kill/base + Snipe +
    // Swoop at once (Primary/Secondary/Tertiary are taken - see docs/roles/gooper.md keybind allocation).
    public override BaseKeybind Keybind => Keybinds.ModifierAction;

    /// <inheritdoc />
    /// <remarks>Hidden/disabled until the holder role has unlocked <see cref="GrantableAbility.Swoop"/>.</remarks>
    public override bool Enabled(RoleBehaviour? role)
    {
        return base.Enabled(role) && Role.UnlockedAbilities.HasFlag(GrantableAbility.Swoop);
    }

    public override bool CanUse()
    {
        if (HudManager.Instance.Chat.IsOpenOrOpening || MeetingHud.Instance)
        {
            return false;
        }

        if (PlayerControl.LocalPlayer.HasModifier<GlitchHackedModifier>() ||
            PlayerControl.LocalPlayer.GetModifiers<DisabledModifier>().Any(x => !x.CanUseAbilities))
        {
            return false;
        }

        return (Timer <= 0 && !EffectActive && (!LimitedUses || UsesLeft > 0)) ||
               (EffectActive && Timer <= EffectDuration - 2f);
    }

    public override void ClickHandler()
    {
        if (!CanUse())
        {
            return;
        }

        OnClick();
        Button?.SetDisabled();
        if (EffectActive)
        {
            Timer = Cooldown;
            EffectActive = false;
        }
        else if (HasEffect)
        {
            EffectActive = true;
            Timer = EffectDuration;
        }
        else
        {
            Timer = Cooldown;
        }
    }

    protected override void OnClick()
    {
        if (!EffectActive)
        {
            PlayerControl.LocalPlayer.RpcAddModifier<TModifier>();
            if (LimitedUses)
            {
                UsesLeft--;
                Button?.SetUsesRemaining(UsesLeft);
            }
        }
        else
        {
            OnEffectEnd();
        }
    }

    public override void OnEffectEnd()
    {
        if (!PlayerControl.LocalPlayer.HasModifier<TModifier>())
        {
            return;
        }

        PlayerControl.LocalPlayer.RpcRemoveModifier<TModifier>();
    }
}

/// <summary>
/// Aim-and-fire piercing shot, gated by <see cref="GrantableAbility.Snipe"/>. All hit math is reused
/// unchanged from <see cref="SniperShots"/> (already role-agnostic machinery); only the freeze/aim-lock
/// bookkeeping is duplicated per role, since it's local per-button state. A concrete subclass must be
/// wired into <see cref="Patches.SniperAimPatch"/>'s per-rendered-frame postfix by calling its own
/// <see cref="HandleAimFrame"/> - MiraAPI button FixedUpdate can't see individual mouse clicks (see
/// docs/il2cpp-gotchas.md's "mouse clicks must be polled per rendered frame" entry). Easy to forget when
/// wiring up a new pool ability; both docs/roles/gooper.md and docs/roles/kirby.md call this out.
/// </summary>
[MiraIgnore]
public abstract class GrantedSnipeButtonBase<TRole> : TownOfUsRoleButton<TRole>
    where TRole : RoleBehaviour, IAbilityGrantHolder
{
    private int armedFrame;
    private bool aimLockActive;

    public override string Name => TouLocale.GetParsed("SuperSquadRoleSniperSnipe", "Snipe");

    // Tertiary, not Secondary: Gooper's Goop and Kirby's Kill already sit on Secondary, and a Snipe can
    // coexist with those, so it needs its own keybind (see the keybind allocation in docs/roles/gooper.md).
    public override BaseKeybind Keybind => Keybinds.TertiaryAction;

    /// <inheritdoc />
    /// <remarks>
    /// Hidden/disabled until the holder role has unlocked <see cref="GrantableAbility.Snipe"/>; stays
    /// enabled while an aim window is pending so a death mid-aim can still clean up (short-circuits
    /// before touching <see cref="TownOfUsRoleButton{TRole}.Role"/> once <c>role is TRole</c> is
    /// false, so this never dereferences a null role post-death - RcXdDeployButton.Enabled pattern).
    /// </remarks>
    public override bool Enabled(RoleBehaviour? role)
    {
        return (base.Enabled(role) && Role.UnlockedAbilities.HasFlag(GrantableAbility.Snipe)) ||
               EffectActive || aimLockActive;
    }

    public override bool CanUse()
    {
        if (HudManager.Instance.Chat.IsOpenOrOpening || MeetingHud.Instance)
        {
            return false;
        }

        return base.CanUse() && !EffectActive && Role.UnlockedAbilities.HasFlag(GrantableAbility.Snipe);
    }

    protected override void OnClick()
    {
        armedFrame = Time.frameCount;
        BeginAim();
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        if (EffectActive && (playerControl.HasDied() || MeetingHud.Instance))
        {
            EffectActive = false;
            SetTimer(Cooldown);
            EndAim();
        }
    }

    public override void OnEffectEnd()
    {
        EndAim();
    }

    /// <summary>
    /// Called every rendered frame (see class remarks) while this button's aim window is active.
    /// </summary>
    public void HandleAimFrame()
    {
        var sniper = PlayerControl.LocalPlayer;
        if (!EffectActive || sniper == null || sniper.HasDied() || MeetingHud.Instance)
        {
            return;
        }

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

        if (Time.frameCount == armedFrame)
        {
            return;
        }

        if (HudManager.Instance.Chat.IsOpenOrOpening)
        {
            return;
        }

        if (MapBehaviour.Instance && MapBehaviour.Instance.IsOpen)
        {
            return;
        }

        if (IsClickOnHud())
        {
            return;
        }

        if (sniper.HasModifier<GlitchHackedModifier>() || sniper.HasModifier<DisabledModifier>())
        {
            return;
        }

        if (Camera.main == null)
        {
            return;
        }

        Fire(sniper);
    }

    private static bool IsClickOnHud()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }

        var uiCamera = HudManager.Instance.UICamera;
        if (uiCamera == null)
        {
            return false;
        }

        var uiPoint = (Vector2)uiCamera.ScreenToWorldPoint(Input.mousePosition);
        var hit = Physics2D.OverlapPoint(uiPoint, LayerMask.GetMask("UI"));
        return hit != null && hit.GetComponentInParent<PassiveButton>() != null;
    }

    private void Fire(PlayerControl sniper)
    {
        var origin = SniperShots.GetShotOrigin(sniper);
        var clickPoint = (Vector2)Camera.main!.ScreenToWorldPoint(Input.mousePosition);
        var direction = (clickPoint - origin).normalized;

        if (direction == Vector2.zero)
        {
            return;
        }

        EffectActive = false;
        SetTimer(Cooldown);
        EndAim();

        var victims = SniperShots.FindHits(sniper, origin, direction, true);
        if (victims.Count > 0)
        {
            sniper.RpcSpecialMultiMurder(victims, true, teleportMurderer: false, playKillSound: true,
                causeOfDeath: "SuperSquadSniper");
        }
    }

    private void BeginAim()
    {
        var sniper = PlayerControl.LocalPlayer;
        sniper.moveable = false;
        sniper.MyPhysics.ResetMoveState();
        sniper.NetTransform.SetPaused(true);
        HudManager.Instance.ShadowQuad.gameObject.SetActive(false);
        aimLockActive = true;
    }

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
        HudManager.Instance.ShadowQuad.gameObject.SetActive(!sniper.Data.IsDead);
    }
}
