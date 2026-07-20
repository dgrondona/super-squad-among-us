using MiraAPI.GameOptions;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Buttons;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Options.Roles.Neutral;
using SuperSquadAmongUs.Roles.Neutral;
using TownOfUs.Modules.Localization;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Neutral;

/// <summary>
/// Gooper's granted Swoop, one of the two v1 pool abilities (3rd goop and beyond). Toggle logic lives
/// in <see cref="GrantedSwoopButtonBase{TRole,TModifier}"/>; the concealment itself is
/// <see cref="GooperSwoopModifier"/>.
/// </summary>
public sealed class GooperSwoopButton : GrantedSwoopButtonBase<GooperRole, GooperSwoopModifier>
{
    public override string Name => TouLocale.GetParsed("SuperSquadRoleGooperSwoop", "Swoop");
    public override Color TextOutlineColor => SuperSquadColors.Gooper;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<GooperOptions>.Instance.SwoopCooldown + MapCooldown, 5f, 120f);

    public override float EffectDuration => OptionGroupSingleton<GooperOptions>.Instance.SwoopDuration;
    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.NeutralPlaceholderButton;
}
