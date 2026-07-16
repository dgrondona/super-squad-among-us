using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using Reactor.Utilities.Extensions;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modules;
using SuperSquadAmongUs.Options.Roles.Neutral;
using SuperSquadAmongUs.Roles.Neutral;
using TownOfUs;
using TownOfUs.Buttons;
using TownOfUs.Modules.Localization;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Neutral;

/// <summary>
/// The Vulture's eat (TOR): consume a nearby dead body - it's removed for everyone and counts
/// toward the win threshold. While alive, blue arrows point at every dead body on the map (TOR's
/// hardcoded blue, local to the Vulture only), toggleable via options.
/// </summary>
public sealed class VultureEatButton : TownOfUsRoleButton<VultureRole, DeadBody>
{
    private readonly Dictionary<byte, ArrowBehaviour> bodyArrows = new();

    public override string Name => TouLocale.GetParsed("SuperSquadRoleVultureEat", "Eat");
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;
    public override Color TextOutlineColor => SuperSquadColors.Vulture;
    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<VultureOptions>.Instance.EatCooldown + MapCooldown, 5f, 120f);
    public override LoadableAsset<Sprite> Sprite => SuperSquadNeutAssets.VultureEatSprite;

    protected override void OnClick()
    {
        if (Target == null)
        {
            return;
        }

        SuperSquadBodies.RpcVultureEat(PlayerControl.LocalPlayer, Target.ParentId);
    }

    public override DeadBody? GetTarget()
    {
        return PlayerControl.LocalPlayer.GetNearestDeadBody(Distance);
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        if (playerControl.HasDied() || MeetingHud.Instance ||
            !OptionGroupSingleton<VultureOptions>.Instance.ShowBodyArrows)
        {
            ClearArrows();
            return;
        }

        SyncArrows(playerControl);
    }

    /// <summary>
    /// Cleans up the body arrows when the local player stops being a living Vulture (death swaps the
    /// role to the ghost role, which also stops this button's FixedUpdate from running).
    /// </summary>
    /// <param name="visible">Whether the hud is visible.</param>
    /// <param name="role">The local player's current role.</param>
    public override void SetActive(bool visible, RoleBehaviour role)
    {
        base.SetActive(visible, role);

        if (role is not VultureRole)
        {
            ClearArrows();
        }
    }

    // One arrow per dead body, created/removed as bodies appear and disappear (vanilla destroys
    // bodies at meeting start; eats remove them mid-round).
    private void SyncArrows(PlayerControl vulture)
    {
        var seen = new HashSet<byte>();
        foreach (var body in UnityEngine.Object.FindObjectsOfType<DeadBody>())
        {
            if (body == null)
            {
                continue;
            }

            seen.Add(body.ParentId);
            if (!bodyArrows.TryGetValue(body.ParentId, out var arrow) || arrow == null)
            {
                arrow = MiscUtils.CreateArrow(vulture.transform, Color.blue);
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
