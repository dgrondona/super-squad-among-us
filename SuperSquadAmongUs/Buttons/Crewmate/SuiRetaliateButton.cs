using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Modifiers;
using MiraAPI.Networking;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Options.Roles.Crewmate;
using SuperSquadAmongUs.Roles.Crewmate;
using TownOfUs.Buttons;
using TownOfUs.Modifiers;
using TownOfUs.Modules.Localization;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Crewmate;

/// <summary>
/// Sui's one-shot retaliation kill, armed by <see cref="Events.SuiEvents"/> when someone interacts
/// with the Sui's protected target. Hidden/disabled until armed. Locked to the actual interactor
/// unless "kill anyone" is on; an arrow points at the locked interactor either way. Clears on use or
/// at the next meeting (confirmed design decision - no separate countdown timer).
/// </summary>
public sealed class SuiRetaliateButton : SuperSquadRoleButton<SuiRole, PlayerControl>, IKillButton
{
    private PlayerControl? lockedTarget;
    private ArrowBehaviour? arrow;

    public override string Name => TouLocale.GetParsed("SuperSquadRoleSuiRetaliate", "Retaliate");
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => SuperSquadColors.Sui;
    public override float Cooldown => 0f;
    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.CrewmatePlaceholderButton;

    /// <summary>
    /// Arms the retaliation window against <paramref name="interactor"/>. Re-arming (a second
    /// interaction before the first is used) replaces the locked target with the latest interactor.
    /// </summary>
    /// <param name="interactor">The player who interacted with Sui's protected target.</param>
    public void Arm(PlayerControl interactor)
    {
        lockedTarget = interactor;
    }

    /// <inheritdoc />
    public override bool Enabled(RoleBehaviour? role)
    {
        return base.Enabled(role) && lockedTarget != null;
    }

    public override bool CanUse()
    {
        if (HudManager.Instance.Chat.IsOpenOrOpening || MeetingHud.Instance)
        {
            return false;
        }

        return lockedTarget != null && !lockedTarget.HasDied() && !PlayerControl.LocalPlayer.HasDied();
    }

    public override PlayerControl? GetTarget()
    {
        if (lockedTarget == null || lockedTarget.HasDied())
        {
            return null;
        }

        return OptionGroupSingleton<SuiOptions>.Instance.CanTargetAnyone
            ? PlayerControl.LocalPlayer.GetClosestLivingPlayer(true, Distance)
            : lockedTarget;
    }

    protected override void OnClick()
    {
        if (Target == null)
        {
            return;
        }

        // Matches Veteran's own retaliation-kill guard: without this, killing an invulnerable target
        // (e.g. Pestilence) can trigger its own counter-kill and softlock the game.
        if (Target.HasModifier<InvulnerabilityModifier>())
        {
            return;
        }

        var options = OptionGroupSingleton<SuiOptions>.Instance;
        var correctTarget = lockedTarget != null && Target.PlayerId == lockedTarget.PlayerId;

        PlayerControl.LocalPlayer.RpcCustomMurder(Target);

        // Sheriff misfire pattern: only meaningful when CanTargetAnyone is on - with it off, Target is
        // always the locked interactor, so correctTarget is always true.
        if (options.CanTargetAnyone && options.DiesOnWrongTarget.Value && !correctTarget)
        {
            PlayerControl.LocalPlayer.RpcCustomMurder(PlayerControl.LocalPlayer);
        }

        lockedTarget = null;
    }

    public override void SetActive(bool visible, RoleBehaviour role)
    {
        base.SetActive(visible, role);

        if (role is not SuiRole)
        {
            ClearArrow();
        }
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        if (MeetingHud.Instance)
        {
            lockedTarget = null;
            ClearArrow();
            return;
        }

        if (lockedTarget == null || lockedTarget.HasDied() || playerControl.HasDied())
        {
            ClearArrow();
            return;
        }

        arrow ??= MiscUtils.CreateArrow(playerControl.transform, SuperSquadColors.Sui);
        arrow.target = lockedTarget.GetTruePosition();
        arrow.Update();
    }

    private void ClearArrow()
    {
        if (arrow != null)
        {
            UnityEngine.Object.Destroy(arrow.gameObject);
            arrow = null;
        }
    }
}
