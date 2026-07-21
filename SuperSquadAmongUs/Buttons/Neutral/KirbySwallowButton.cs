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

/// <summary>
/// Kirby's Swallow: a near-literal copy of <see cref="PelicanDevourButton"/> - targeted ability on the
/// primary action, range-based like a vanilla kill button. Target any living player, impostors
/// included (neutral killer), excluding anyone already carried.
/// </summary>
public sealed class KirbySwallowButton : TownOfUsRoleButton<KirbyRole, PlayerControl>, IKillButton
{
    public override string Name => TouLocale.GetParsed("SuperSquadRoleKirbySwallow", "Swallow");

    // SecondaryAction, not Primary: Kirby's granted Kill (GrantedKillButton) claims PrimaryAction, and
    // Swallow coexists with it once Kirby has inherited a kill (see docs/roles/gooper.md keybinds).
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => SuperSquadColors.Kirby;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<KirbyOptions>.Instance.SwallowCooldown + MapCooldown, 5f, 120f);

    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.NeutralPlaceholderButton;

    protected override void OnClick()
    {
        if (Target == null)
        {
            return;
        }

        Target.RpcAddModifier<KirbySwallowedModifier>(PlayerControl.LocalPlayer);
    }

    public override PlayerControl? GetTarget()
    {
        return PlayerControl.LocalPlayer.GetClosestLivingPlayer(true, Distance, false,
            x => !x.HasModifier<CarriedModifier>());
    }
}
