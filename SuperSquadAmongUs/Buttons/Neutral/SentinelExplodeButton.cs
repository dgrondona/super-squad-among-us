using Il2CppInterop.Runtime.Attributes;
using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Keybinds;
using MiraAPI.Utilities;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modules;
using SuperSquadAmongUs.Options.Roles.Neutral;
using SuperSquadAmongUs.Roles.Neutral;
using TownOfUs.Assets;
using TownOfUs.Buttons;
using TownOfUs.Buttons.Neutral;
using TownOfUs.Modules;
using TownOfUs.Modules.Localization;
using TownOfUs.Networking;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Neutral;

public sealed class SentinelExplodeButton : TownOfUsRoleButton<SentinelRole>
{
    public override string Name => TouLocale.GetParsed("SuperSquadRoleSentinelExplode", "Explode");
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => SuperSquadColors.Sentinel;
    public override float Cooldown => Math.Clamp(OptionGroupSingleton<SentinelOptions>.Instance.ExplodeCooldown + MapCooldown, 5f, 120f);
    public override LoadableAsset<Sprite> Sprite => SuperSquadNeutAssets.SentinelExplodeSprite;

    private static List<PlayerControl> PlayersInRange => Helpers.GetClosestPlayers(PlayerControl.LocalPlayer,
        OptionGroupSingleton<SentinelOptions>.Instance.ExplosionRadius.Value * ShipStatus.Instance.MaxLightRadius);

    [HideFromIl2Cpp] public Explode? Explode { get; set; }

    public override bool CanUse()
    {
        var count = PlayersInRange.Count;

        if (count > 0 && !PlayerControl.LocalPlayer.HasDied() && Timer <= 0)
        {
            var pos = PlayerControl.LocalPlayer.transform.position;
            pos.z += 0.001f;

            if (Explode == null)
            {
                Explode = Explode.CreateExplode(pos);
            }
            else
            {
                Explode.Transform.localPosition = pos;
            }
        }
        else
        {
            if (Explode != null)
            {
                Explode.Clear();
                Explode = null;
            }
        }

        return base.CanUse() && count > 0;
    }

    protected override void OnClick()
    {
        var dousedPlayers = PlayersInRange.ToList();
        if (dousedPlayers.Count <= 0)
        {
            return;
        }

        PlayerControl.LocalPlayer.RpcSpecialMultiMurder(dousedPlayers, true, teleportMurderer: false,
            playKillSound: false,
            causeOfDeath: "SentinelExplosion");

        TouAudio.PlaySound(TouAudio.ArsoIgniteSound);

        CustomButtonSingleton<SentinelKillButton>.Instance.ResetCooldownAndOrEffect();
    }
}