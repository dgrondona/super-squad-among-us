using MiraAPI.Keybinds;
using MiraAPI.Modifiers;
using MiraAPI.Networking;
using MiraAPI.PluginLoading;
using MiraAPI.Utilities.Assets;
using Reactor.Utilities.Extensions;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Modules;
using SuperSquadAmongUs.Options;
using TownOfUs.Assets;
using TownOfUs.Buttons;
using TownOfUs.Modifiers;
using TownOfUs.Modifiers.Neutral;
using TownOfUs.Modules.Localization;
using TownOfUs.Networking;
using TownOfUs.Utilities;
using UnityEngine;
using UnityEngine.EventSystems;
using MiraAPI.GameOptions;

namespace SuperSquadAmongUs.Buttons;

/// <summary>
/// The role-agnostic granted-ability buttons: ONE button per ability, shown for ANY role that has
/// unlocked that ability's <see cref="GrantableAbility"/> flag (Gooper via goop tiers, Kirby via
/// swallow inheritance - see docs/roles/gooper.md's "Shared ability-grant architecture"). Unlike a
/// role-typed <c>TownOfUsRoleButton&lt;TRole&gt;</c>, these gate <c>Enabled</c>
/// on the unlocked flag instead of a role type (the modifier-gated-button pattern TOU-Mira's own
/// <c>ScientistButton</c> uses), so a new ability costs exactly one button here - no per-(role, ability)
/// subclass, and no need to touch the roles at all. Adding, say, Puppeteer's control is: one flag, one
/// button here, one modifier, and one <see cref="AbilityGrants.GetPortableAbilities"/> entry.
/// </summary>
internal static class GrantedAbility
{
    /// <summary>True if the local player's role has unlocked <paramref name="ability"/>.</summary>
    public static bool Unlocked(GrantableAbility ability)
    {
        return PlayerControl.LocalPlayer?.Data?.Role is IAbilityGrantHolder holder &&
               holder.UnlockedAbilities.HasFlag(ability);
    }

    /// <summary>The local role's colour, used for button/target outlines regardless of which role it is.</summary>
    public static Color OutlineColor =>
        PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.Data?.Role != null
            ? PlayerControl.LocalPlayer.Data.Role.TeamColor
            : Color.clear;
}

/// <summary>
/// Shared plumbing for granted abilities that target a player (Kill, Hide). Reimplements the
/// player-outline and target-validity bits that <c>TownOfUsRoleButton&lt;TRole, TTarget&gt;</c> would
/// normally provide, since these deliberately don't inherit from it (no owning role type).
/// </summary>
[MiraIgnore]
public abstract class GrantedTargetButtonBase : TownOfUsTargetButton<PlayerControl>
{
    protected abstract GrantableAbility RequiredAbility { get; }

    public override Color TextOutlineColor => GrantedAbility.OutlineColor;

    /// <inheritdoc />
    /// <remarks>Shown for any <see cref="IAbilityGrantHolder"/> role that has unlocked the flag.</remarks>
    public override bool Enabled(RoleBehaviour? role)
    {
        return !Disabled && role is IAbilityGrantHolder holder &&
               holder.UnlockedAbilities.HasFlag(RequiredAbility);
    }

    public override void SetOutline(bool active)
    {
        if (Target != null && !PlayerControl.LocalPlayer.HasDied())
        {
            Target.cosmetics.currentBodySprite.BodySprite.SetOutline(active ? GrantedAbility.OutlineColor : null);
        }
    }

    public override bool IsTargetValid(PlayerControl? target)
    {
        return base.IsTargetValid(target) && target != null && !target.inVent &&
               !target.GetModifiers<DisabledModifier>().Any(mod => !mod.CanBeInteractedWith);
    }
}

/// <summary>
/// Granted Kill: a standard kill on the nearest living player. Unlocked by Gooper's 2nd goop or by
/// swallowing a kill-capable victim (Kirby).
/// </summary>
public sealed class GrantedKillButton : GrantedTargetButtonBase, IKillButton
{
    protected override GrantableAbility RequiredAbility => GrantableAbility.Kill;

    public override string Name => TranslationController.Instance.GetStringWithDefault(StringNames.KillLabel, "Kill");
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;
    public override LoadableAsset<Sprite> Sprite => TouAssets.KillSprite;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<GrantedAbilityOptions>.Instance.KillCooldown.Value + MapCooldown, 5f, 120f);

    public override PlayerControl? GetTarget()
    {
        return PlayerControl.LocalPlayer.GetClosestLivingPlayer(true, Distance);
    }

    protected override void OnClick()
    {
        if (Target != null)
        {
            PlayerControl.LocalPlayer.RpcCustomMurder(Target);
        }
    }
}

/// <summary>
/// Granted Hide: cloak a living player exactly like Daddy Hagrid (reuses <see cref="CloakHiddenModifier"/>
/// verbatim, so it IS Hagrid's ability, not a copy). Click-only - the distinct keybind slots are taken
/// (see docs/roles/gooper.md keybind allocation).
/// </summary>
public sealed class GrantedHideButton : GrantedTargetButtonBase
{
    protected override GrantableAbility RequiredAbility => GrantableAbility.Hide;

    public override string Name => TouLocale.GetParsed("SuperSquadRoleGrantedHide", "Hide");
    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.NeutralPlaceholderButton;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<GrantedAbilityOptions>.Instance.HideCooldown.Value + MapCooldown, 5f, 120f);

    public override PlayerControl? GetTarget()
    {
        return PlayerControl.LocalPlayer.GetClosestLivingPlayer(true, Distance, false,
            x => !x.HasModifier<CarriedModifier>());
    }

    protected override void OnClick()
    {
        if (Target != null)
        {
            Target.RpcAddModifier<CloakHiddenModifier>(PlayerControl.LocalPlayer);
        }
    }
}

/// <summary>
/// Granted Vest: pop a temporary protective vest on yourself. Unlocked by Gooper's 1st goop.
/// Click-only. Self-expires (<see cref="GrantedVestModifier"/>), then the cooldown gates the next use.
/// </summary>
public sealed class GrantedVestButton : TownOfUsButton
{
    public override string Name => TouLocale.GetParsed("SuperSquadRoleGrantedVest", "Vest");
    public override Color TextOutlineColor => GrantedAbility.OutlineColor;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<GrantedAbilityOptions>.Instance.VestCooldown.Value + MapCooldown, 5f, 120f);

    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.NeutralPlaceholderButton;

    public override bool Enabled(RoleBehaviour? role)
    {
        return !Disabled && role is IAbilityGrantHolder holder &&
               holder.UnlockedAbilities.HasFlag(GrantableAbility.Vest);
    }

    public override bool CanUse()
    {
        return base.CanUse() && !PlayerControl.LocalPlayer.HasModifier<GrantedVestModifier>();
    }

    protected override void OnClick()
    {
        PlayerControl.LocalPlayer.RpcAddModifier<GrantedVestModifier>();
    }
}

/// <summary>
/// Granted Swoop: toggle a self-only concealment (<see cref="GrantedSwoopModifier"/>), the Swooper's
/// ability. Toggle machinery mirrors TOU-Mira's <c>SwooperSwoopButton</c>.
/// </summary>
public sealed class GrantedSwoopButton : TownOfUsButton
{
    public override string Name => TouLocale.GetParsed("SuperSquadRoleGrantedSwoop", "Swoop");
    public override BaseKeybind Keybind => Keybinds.ModifierAction;
    public override Color TextOutlineColor => GrantedAbility.OutlineColor;
    public override bool ZeroIsInfinite { get; set; } = true;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<GrantedAbilityOptions>.Instance.SwoopCooldown.Value + MapCooldown, 5f, 120f);

    public override float EffectDuration => OptionGroupSingleton<GrantedAbilityOptions>.Instance.SwoopDuration.Value;
    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.NeutralPlaceholderButton;

    public override bool Enabled(RoleBehaviour? role)
    {
        return !Disabled && role is IAbilityGrantHolder holder &&
               holder.UnlockedAbilities.HasFlag(GrantableAbility.Swoop);
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
            PlayerControl.LocalPlayer.RpcAddModifier<GrantedSwoopModifier>();
        }
        else
        {
            OnEffectEnd();
        }
    }

    public override void OnEffectEnd()
    {
        if (PlayerControl.LocalPlayer.HasModifier<GrantedSwoopModifier>())
        {
            PlayerControl.LocalPlayer.RpcRemoveModifier<GrantedSwoopModifier>();
        }
    }
}

/// <summary>
/// Granted Snipe: the Sniper's aim-and-fire piercing shot, reusing <see cref="SniperShots"/> unchanged
/// (already role-agnostic hit math). Per-frame click polling is driven from
/// <see cref="Patches.SniperAimPatch"/> via <see cref="HandleAimFrame"/> (fixed-tick FixedUpdate drops
/// clicks - see docs/il2cpp-gotchas.md).
/// </summary>
public sealed class GrantedSnipeButton : TownOfUsButton
{
    private int armedFrame;
    private bool aimLockActive;

    public override string Name => TouLocale.GetParsed("SuperSquadRoleSniperSnipe", "Snipe");
    public override BaseKeybind Keybind => Keybinds.TertiaryAction;
    public override Color TextOutlineColor => GrantedAbility.OutlineColor;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<GrantedAbilityOptions>.Instance.SnipeCooldown.Value + MapCooldown, 5f, 120f);

    public override float EffectDuration => OptionGroupSingleton<GrantedAbilityOptions>.Instance.AimWindow.Value;
    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.NeutralPlaceholderButton;

    public override bool Enabled(RoleBehaviour? role)
    {
        // Stay enabled while an aim window/lock is pending so a death mid-aim can still clean up.
        return (!Disabled && role is IAbilityGrantHolder holder &&
                holder.UnlockedAbilities.HasFlag(GrantableAbility.Snipe)) || EffectActive || aimLockActive;
    }

    public override bool CanUse()
    {
        if (HudManager.Instance.Chat.IsOpenOrOpening || MeetingHud.Instance)
        {
            return false;
        }

        return base.CanUse() && !EffectActive && GrantedAbility.Unlocked(GrantableAbility.Snipe);
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

    /// <summary>Called every rendered frame (see class remarks) while this button's aim window is active.</summary>
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

        if (!Input.GetMouseButtonDown(0) || Time.frameCount == armedFrame)
        {
            return;
        }

        if (HudManager.Instance.Chat.IsOpenOrOpening ||
            (MapBehaviour.Instance && MapBehaviour.Instance.IsOpen) || IsClickOnHud())
        {
            return;
        }

        if (sniper.HasModifier<GlitchHackedModifier>() || sniper.HasModifier<DisabledModifier>() || Camera.main == null)
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
