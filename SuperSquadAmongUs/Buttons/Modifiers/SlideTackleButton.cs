using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Keybinds;
using MiraAPI.Modifiers;
using MiraAPI.Utilities.Assets;
using Reactor.Utilities.Extensions;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Options.Modifiers;
using TownOfUs.Buttons;
using TownOfUs.Modifiers;
using TownOfUs.Modules.Localization;
using TownOfUs.Roles.Other;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Modifiers;

/// <summary>
/// Ability button granted by the <see cref="SlideTackleModifier"/>: lunge to the nearest player and
/// stun them with a <see cref="TackledModifier"/>. The lunge is a position snap for now - a slide
/// animation (and the victim falling on their face) comes later.
/// </summary>
public sealed class SlideTackleButton : TownOfUsTargetButton<PlayerControl>
{
    /// <inheritdoc />
    public override string Name => TouLocale.GetParsed("SuperSquadModifierSlideTackleButton", "Tackle");

    /// <inheritdoc />
    public override BaseKeybind Keybind => Keybinds.ModifierAction;

    /// <inheritdoc />
    public override Color TextOutlineColor => SuperSquadColors.SlideTackle;

    /// <inheritdoc />
    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<SlideTackleOptions>.Instance.Cooldown + MapCooldown, 5f, 120f);

    /// <inheritdoc />
    public override ButtonLocation Location => ButtonLocation.BottomLeft;

    /// <inheritdoc />
    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.NeutralPlaceholderButton;

    /// <inheritdoc />
    public override bool Enabled(RoleBehaviour? role)
    {
        return PlayerControl.LocalPlayer &&
               PlayerControl.LocalPlayer.HasModifier<SlideTackleModifier>() &&
               !PlayerControl.LocalPlayer.Data.IsDead;
    }

    /// <inheritdoc />
    public override PlayerControl? GetTarget()
    {
        // Anyone in range is fair game, impostors included; already-tackled players are skipped.
        return PlayerControl.LocalPlayer.GetClosestLivingPlayer(true, Distance, false,
            x => !x.HasModifier<TackledModifier>());
    }

    /// <inheritdoc />
    public override bool IsTargetValid(PlayerControl? target)
    {
        // TownOfUsTargetButton doesn't carry TownOfUsRoleButton's player-target checks; replicate
        // them: no venting targets, nobody flagged untargetable, no spectators.
        return base.IsTargetValid(target) && target != null && !target.inVent &&
               !target.GetModifiers<DisabledModifier>().Any(mod => !mod.CanBeInteractedWith) &&
               !SpectatorRole.TrackedSpectators.Contains(target.Data.PlayerName);
    }

    /// <inheritdoc />
    public override void SetOutline(bool active)
    {
        if (Target != null && !PlayerControl.LocalPlayer.HasDied())
        {
            Target.cosmetics.currentBodySprite.BodySprite.SetOutline(active ? SuperSquadColors.SlideTackle : null);
        }
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        if (Target == null)
        {
            return;
        }

        var landingPosition = Target.GetTruePosition();
        Target.RpcAddModifier<TackledModifier>();
        PlayerControl.LocalPlayer.NetTransform.RpcSnapTo(landingPosition);
    }
}
