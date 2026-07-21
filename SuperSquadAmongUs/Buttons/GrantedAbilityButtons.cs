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
using TownOfUs.Utilities;
using UnityEngine;
using MiraAPI.GameOptions;

namespace SuperSquadAmongUs.Buttons;

/// <summary>
/// The granted-ability PRIMITIVE buttons: one button per <see cref="GrantableAbility"/> flag, shown for
/// ANY role that has unlocked it (the modifier-gated-button pattern TOU-Mira's own
/// <c>ScientistButton</c> uses). These exist only for abilities with no borrowable source button in
/// this addon: vanilla Kill, the TOU-Mira Swooper's Swoop, and Gooper's Vest. Abilities sourced from
/// this addon's own roles are NOT recreated here - they transfer as whole button kits via
/// <see cref="IAbilityGrantHolder.GrantedKits"/> and <c>SuperSquadRoleButton</c>'s grant-aware
/// <c>Enabled</c>, so the borrower gets the source role's real button (see docs/architecture.md's
/// granting section).
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
/// Shared plumbing for granted primitives that target a player (currently just Kill). Reimplements the
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

