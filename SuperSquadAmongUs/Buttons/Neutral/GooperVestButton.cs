using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Modules;
using SuperSquadAmongUs.Options.Roles.Neutral;
using SuperSquadAmongUs.Roles.Neutral;
using TownOfUs.Buttons;
using TownOfUs.Modules.Localization;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Neutral;

/// <summary>
/// Gooper's Vest, unlocked on the 1st goop. Pressing it pops a temporary protective vest on the Gooper
/// (<see cref="GooperVestModifier"/>, which self-expires after the configured duration and is blocked
/// from stacking while already active), then goes on cooldown. Click-only, no keybind: the Gooper's
/// other simultaneous abilities (Kill/Goop/Snipe/Swoop) already claim every distinct action keybind
/// (see the keybind allocation in docs/roles/gooper.md).
/// </summary>
public sealed class GooperVestButton : TownOfUsRoleButton<GooperRole>
{
    public override string Name => TouLocale.GetParsed("SuperSquadRoleGooperVest", "Vest");
    public override Color TextOutlineColor => SuperSquadColors.Gooper;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<GooperOptions>.Instance.VestCooldown + MapCooldown, 5f, 120f);

    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.NeutralPlaceholderButton;

    /// <inheritdoc />
    /// <remarks>Hidden/disabled until the 1st goop unlocks <see cref="GrantableAbility.Vest"/>.</remarks>
    public override bool Enabled(RoleBehaviour? role)
    {
        return base.Enabled(role) && Role.UnlockedAbilities.HasFlag(GrantableAbility.Vest);
    }

    public override bool CanUse()
    {
        // Don't re-pop while a vest is still active - the modifier self-expires, then the cooldown gates
        // the next use.
        return base.CanUse() && !PlayerControl.LocalPlayer.HasModifier<GooperVestModifier>();
    }

    protected override void OnClick()
    {
        PlayerControl.LocalPlayer.RpcAddModifier<GooperVestModifier>();
    }
}
