using MiraAPI.GameOptions;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Buttons;
using SuperSquadAmongUs.Options.Roles.Neutral;
using SuperSquadAmongUs.Roles.Neutral;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Neutral;

/// <summary>
/// Gooper's granted Snipe, one of the two v1 pool abilities (3rd goop and beyond). Aim/fire logic
/// lives in <see cref="GrantedSnipeButtonBase{TRole}"/>; must also be wired into
/// <see cref="Patches.SniperAimPatch"/> for per-frame click polling.
/// </summary>
public sealed class GooperSnipeButton : GrantedSnipeButtonBase<GooperRole>
{
    public override Color TextOutlineColor => SuperSquadColors.Gooper;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<GooperOptions>.Instance.SnipeCooldown + MapCooldown, 5f, 120f);

    public override float EffectDuration => OptionGroupSingleton<GooperOptions>.Instance.AimWindow;
    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.NeutralPlaceholderButton;
}
