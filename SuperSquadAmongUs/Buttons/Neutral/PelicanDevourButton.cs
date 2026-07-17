using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Modifiers;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Options.Roles.Neutral;
using SuperSquadAmongUs.Roles.Neutral;
using TownOfUs.Buttons;
using TownOfUs.Modules.Localization;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Neutral;

public sealed class PelicanDevourButton : TownOfUsRoleButton<PelicanRole, PlayerControl>, IKillButton
{
    public override string Name => TouLocale.GetParsed("SuperSquadRolePelicanDevour", "Devour");
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;
    public override Color TextOutlineColor => SuperSquadColors.Pelican;
    public override float Cooldown => Math.Clamp(OptionGroupSingleton<PelicanOptions>.Instance.DevourCooldown + MapCooldown, 5f, 120f);
    public override LoadableAsset<Sprite> Sprite => SuperSquadNeutAssets.PelicanDevourSprite;

    protected override void OnClick()
    {
        if (Target == null)
        {
            return;
        }

        Target.RpcAddModifier<DevouredModifier>(PlayerControl.LocalPlayer);
    }

    public override PlayerControl? GetTarget()
    {
        // The Pelican is a neutral killer: anyone is food, impostors included - but not someone
        // already carried (devoured or hidden in a Hagrid's cloak).
        return PlayerControl.LocalPlayer.GetClosestLivingPlayer(true, Distance, false,
            x => !x.HasModifier<CarriedModifier>());
    }
}
