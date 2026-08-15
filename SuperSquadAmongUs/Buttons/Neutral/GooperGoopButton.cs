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
public sealed class GooperGoopButton : SuperSquadRoleButton<GooperRole, DeadBody>
{
    private readonly Dictionary<byte, ArrowBehaviour> bodyArrows = new();

    public override string Name => TouLocale.GetParsed("SuperSquadRoleGooperGoop", "Goop");

    // TertiaryAction: PrimaryAction belongs to the granted Kill, and borrowed kit buttons keep their
    // source keybinds - which is almost always SecondaryAction - so the Gooper's own core ability
    // lives on the one slot borrowed kits rarely use (see docs/roles/gooper.md keybind allocation).
    public override BaseKeybind Keybind => Keybinds.TertiaryAction;
    public override Color TextOutlineColor => SuperSquadColors.Gooper;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<GooperOptions>.Instance.GoopCooldown + MapCooldown, 5f, 120f);

    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.NeutralPlaceholderButton;

    public override DeadBody? GetTarget()
    {
        // Can't use the plain GetNearestDeadBody(Distance) helper here (see VultureEatButton) - it
        // has no way to exclude already-gooped bodies from the search, so a closer gooped body would
        // shadow a farther un-gooped one and IsTargetValid would reject the result, leaving Target
        // null even though a legitimate target exists. GetNearestObjectOfType's predicate lets gooped
        // bodies be skipped during the search itself instead of after a body is already chosen.
        return PlayerControl.LocalPlayer.GetNearestObjectOfType<DeadBody>(
            Distance,
            Helpers.CreateFilter(Constants.NotShipMask),
            "DeadBody",
            body => body && !body.Reported && !Role.GoopedBodyIds.Contains(body.ParentId));
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

        // The 3rd goop and beyond each draw one pool entry, without replacement (confirmed design
        // decision) - picked here, on the Gooper's own client, so every client applies the same draw
        // once the RPC arrives (same split ElusiveEvents uses for its teleport destination).
        var poolIndex = Role.GoopedBodyIds.Count >= 2
            ? AbilityGrants.PickRandomPoolIndex(Role)
            : AbilityGrants.NoPoolChoice;

        SuperSquadGooper.RpcGoop(PlayerControl.LocalPlayer, Target.ParentId, poolIndex);
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
