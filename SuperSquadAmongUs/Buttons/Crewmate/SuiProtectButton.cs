using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Modifiers;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Options.Roles.Crewmate;
using SuperSquadAmongUs.Roles.Crewmate;
using TownOfUs.Buttons;
using TownOfUs.Modules.Localization;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Crewmate;

/// <summary>
/// Sui's Protect: mark one living player as protected (see <see cref="SuiProtectedModifier"/>). Can
/// retarget to a different player later - the previous protection is dropped first, since the Sui can
/// only protect one person at a time.
/// </summary>
public sealed class SuiProtectButton : TownOfUsRoleButton<SuiRole, PlayerControl>
{
    public override string Name => TouLocale.GetParsed("SuperSquadRoleSuiProtect", "Protect");
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;
    public override Color TextOutlineColor => SuperSquadColors.Sui;

    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<SuiOptions>.Instance.ProtectCooldown + MapCooldown, 5f, 120f);

    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.CrewmatePlaceholderButton;

    public override PlayerControl? GetTarget()
    {
        return PlayerControl.LocalPlayer.GetClosestLivingPlayer(true, Distance, false,
            x => !x.HasModifier<SuiProtectedModifier>());
    }

    protected override void OnClick()
    {
        if (Target == null)
        {
            Error("Sui Protect: Target is null");
            return;
        }

        if (Role.Protected != null && Role.Protected.HasModifier<SuiProtectedModifier>())
        {
            Role.Protected.RpcRemoveModifier<SuiProtectedModifier>();
        }

        Target.RpcAddModifier<SuiProtectedModifier>(PlayerControl.LocalPlayer);
        Role.Protected = Target;
    }
}
