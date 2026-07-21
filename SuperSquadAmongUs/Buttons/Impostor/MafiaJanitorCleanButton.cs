using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modules;
using SuperSquadAmongUs.Options.Roles.Impostor;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs;
using TownOfUs.Buttons;
using TownOfUs.Modules.Localization;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Impostor;

/// <summary>
/// The Mafia Janitor's clean (TOR): instantly removes a nearby dead body for everyone. Unlike
/// TOU-Mira's standalone Janitor there is no clean delay, no use limit, and no kill button at all -
/// TOR's mafia janitor only cleans.
/// </summary>
public sealed class MafiaJanitorCleanButton : SuperSquadRoleButton<MafiaJanitorRole, DeadBody>
{
    public override string Name => TouLocale.GetParsed("SuperSquadRoleMafiaJanitorClean", "Clean");
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => TownOfUsColors.Impostor;
    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<MafiaOptions>.Instance.JanitorCleanCooldown + MapCooldown, 5f, 120f);
    public override LoadableAsset<Sprite> Sprite => SuperSquadImpAssets.MafiaJanitorCleanSprite;

    /// <inheritdoc />
    public override void CreateButton(Transform parent)
    {
        base.CreateButton(parent);

        if (Button?.buttonLabelText != null)
        {
            Button.buttonLabelText.gameObject.SetActive(false);
        }
    }

    protected override void OnClick()
    {
        if (Target == null)
        {
            return;
        }

        SuperSquadBodies.RpcMafiaCleanBody(PlayerControl.LocalPlayer, Target.ParentId);
    }

    public override DeadBody? GetTarget()
    {
        return PlayerControl.LocalPlayer.GetNearestDeadBody(Distance);
    }
}
