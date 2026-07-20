using MiraAPI.GameOptions;
using SuperSquadAmongUs.Buttons;
using SuperSquadAmongUs.Options.Roles.Neutral;
using SuperSquadAmongUs.Roles.Neutral;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Neutral;

/// <summary>
/// Gooper's granted Kill, unlocked on the 2nd goop. Targeting/click logic, keybind (PrimaryAction) and
/// the standard kill sprite all live in <see cref="GrantedKillButtonBase{TRole}"/>.
/// </summary>
public sealed class GooperKillButton : GrantedKillButtonBase<GooperRole>
{
    public override Color TextOutlineColor => SuperSquadColors.Gooper;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<GooperOptions>.Instance.KillCooldown + MapCooldown, 5f, 120f);
}
