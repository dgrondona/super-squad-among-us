using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using SuperSquadAmongUs.Buttons;
using SuperSquadAmongUs.Options.Roles.Neutral;
using SuperSquadAmongUs.Roles.Neutral;
using TownOfUs.Buttons;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Neutral;

/// <summary>
/// Kirby's granted Kill (inherited by swallowing a kill-capable victim). Targeting/click logic and the
/// standard kill sprite live in <see cref="GrantedKillButtonBase{TRole}"/>; the keybind moves to
/// SecondaryAction here because Kirby's Swallow already owns PrimaryAction.
/// </summary>
public sealed class KirbyKillButton : GrantedKillButtonBase<KirbyRole>
{
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => SuperSquadColors.Kirby;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<KirbyOptions>.Instance.KillCooldown + MapCooldown, 5f, 120f);
}
