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
/// Kirby's granted Swoop, inherited by digesting a Swooper. Toggle logic lives in
/// <see cref="GrantedSwoopButtonBase{TRole,TModifier}"/>; the concealment itself is
/// <see cref="KirbySwoopModifier"/>.
/// </summary>
public sealed class KirbySwoopButton : GrantedSwoopButtonBase<KirbyRole, KirbySwoopModifier>
{
    public override string Name => TouLocale.GetParsed("SuperSquadRoleKirbySwoop", "Swoop");
    public override Color TextOutlineColor => SuperSquadColors.Kirby;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<KirbyOptions>.Instance.SwoopCooldown + MapCooldown, 5f, 120f);

    public override float EffectDuration => OptionGroupSingleton<KirbyOptions>.Instance.SwoopDuration;
    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.NeutralPlaceholderButton;
}
