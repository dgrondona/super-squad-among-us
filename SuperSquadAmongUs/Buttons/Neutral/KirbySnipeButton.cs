using MiraAPI.GameOptions;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Buttons;
using SuperSquadAmongUs.Options.Roles.Neutral;
using SuperSquadAmongUs.Roles.Neutral;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Neutral;

/// <summary>
/// Kirby's granted Snipe, inherited by digesting a Sniper. Aim/fire logic lives in
/// <see cref="GrantedSnipeButtonBase{TRole}"/>; must also be wired into
/// <see cref="Patches.SniperAimPatch"/> for per-frame click polling.
/// </summary>
public sealed class KirbySnipeButton : GrantedSnipeButtonBase<KirbyRole>
{
    public override Color TextOutlineColor => SuperSquadColors.Kirby;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<KirbyOptions>.Instance.SnipeCooldown + MapCooldown, 5f, 120f);

    public override float EffectDuration => OptionGroupSingleton<KirbyOptions>.Instance.AimWindow;
    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.NeutralPlaceholderButton;
}
