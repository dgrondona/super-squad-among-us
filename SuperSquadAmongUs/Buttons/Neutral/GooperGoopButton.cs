using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modules;
using SuperSquadAmongUs.Options.Roles.Neutral;
using SuperSquadAmongUs.Roles.Neutral;
using TownOfUs.Buttons;
using TownOfUs.Modules.Localization;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Neutral;

/// <summary>
/// The Gooper's Goop: mark a nearby dead body as gooped (not destroyed - it stays on the map and can
/// still be reported or cleaned by other roles). Each successful goop past the first grants an
/// escalating power - see <see cref="SuperSquadGooper.RpcGoop"/>. An arrow points at every un-gooped
/// body while alive, mirroring the Vulture's eat button.
/// </summary>
public sealed class GooperGoopButton : TownOfUsRoleButton<GooperRole, DeadBody>
{
    private readonly Dictionary<byte, ArrowBehaviour> bodyArrows = new();

    public override string Name => TouLocale.GetParsed("SuperSquadRoleGooperGoop", "Goop");

    // SecondaryAction, not Primary: the granted Kill (GrantedKillButton) claims PrimaryAction, and Goop
    // coexists with it once the Gooper has gooped twice (see docs/roles/gooper.md keybind allocation).
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => SuperSquadColors.Gooper;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<GooperOptions>.Instance.GoopCooldown + MapCooldown, 5f, 120f);

    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.NeutralPlaceholderButton;

    public override DeadBody? GetTarget()
    {
        return PlayerControl.LocalPlayer.GetNearestDeadBody(Distance);
    }

    public override bool IsTargetValid(DeadBody? target)
    {
        return target != null && !Role.GoopedBodyIds.Contains(target.ParentId);
    }

    protected override void OnClick()
    {
        if (Target == null)
        {
            return;
        }

        // The 3rd goop and beyond each draw one pool ability, without replacement (confirmed design
        // decision) - picked here, on the Gooper's own client, so every client applies the same draw
        // once the RPC arrives (same split ElusiveEvents uses for its teleport destination).
        var poolAbility = Role.GoopedBodyIds.Count >= 2
            ? AbilityGrants.PickRandomPoolAbility(Role.UnlockedAbilities)
            : GrantableAbility.None;

        SuperSquadGooper.RpcGoop(PlayerControl.LocalPlayer, Target.ParentId, (byte)poolAbility);
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        if (playerControl.HasDied() || MeetingHud.Instance)
        {
            ClearArrows();
            return;
        }

        SyncArrows(playerControl);
    }

    /// <summary>
    /// Cleans up the body arrows when the local player stops being a living Gooper (death swaps the
    /// role to the ghost role, which also stops this button's FixedUpdate from running).
    /// </summary>
    /// <param name="visible">Whether the hud is visible.</param>
    /// <param name="role">The local player's current role.</param>
    public override void SetActive(bool visible, RoleBehaviour role)
    {
        base.SetActive(visible, role);

        if (role is not GooperRole)
        {
            ClearArrows();
        }
    }

    // One arrow per un-gooped dead body, created/removed as bodies appear, get gooped, or disappear.
    private void SyncArrows(PlayerControl gooper)
    {
        var seen = new HashSet<byte>();
        foreach (var body in UnityEngine.Object.FindObjectsOfType<DeadBody>())
        {
            if (body == null || Role.GoopedBodyIds.Contains(body.ParentId))
            {
                continue;
            }

            seen.Add(body.ParentId);
            if (!bodyArrows.TryGetValue(body.ParentId, out var arrow) || arrow == null)
            {
                arrow = MiscUtils.CreateArrow(gooper.transform, SuperSquadColors.Gooper);
                bodyArrows[body.ParentId] = arrow;
            }

            arrow.target = body.TruePosition;
            arrow.Update();
        }

        foreach (var parentId in bodyArrows.Keys.Where(x => !seen.Contains(x)).ToList())
        {
            DestroyArrow(bodyArrows[parentId]);
            bodyArrows.Remove(parentId);
        }
    }

    private void ClearArrows()
    {
        if (bodyArrows.Count == 0)
        {
            return;
        }

        foreach (var arrow in bodyArrows.Values)
        {
            DestroyArrow(arrow);
        }

        bodyArrows.Clear();
    }

    private static void DestroyArrow(ArrowBehaviour? arrow)
    {
        if (arrow != null)
        {
            UnityEngine.Object.Destroy(arrow.gameObject);
        }
    }
}
