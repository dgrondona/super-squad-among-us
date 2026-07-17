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
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Crewmate;

/// <summary>
/// The Elusive's Shield ability (VeteranAlertButton clone): raises <see cref="ElusiveShieldModifier"/>
/// for the configured duration; the button's effect fill mirrors the shield and the cooldown starts
/// when it drops.
/// </summary>
public sealed class ElusiveShieldButton : TownOfUsRoleButton<ElusiveRole>
{
    /// <inheritdoc />
    public override string Name => TouLocale.GetParsed("SuperSquadRoleElusiveShield", "Shield");

    /// <inheritdoc />
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;

    /// <inheritdoc />
    public override Color TextOutlineColor => SuperSquadColors.Elusive;

    /// <inheritdoc />
    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<ElusiveOptions>.Instance.ShieldCooldown + MapCooldown, 5f, 120f);

    /// <inheritdoc />
    public override float EffectDuration => OptionGroupSingleton<ElusiveOptions>.Instance.ShieldDuration;

    /// <inheritdoc />
    public override int MaxUses => (int)OptionGroupSingleton<ElusiveOptions>.Instance.MaxShields;

    /// <inheritdoc />
    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.CrewmatePlaceholderButton;

    /// <inheritdoc />
    protected override void OnClick()
    {
        PlayerControl.LocalPlayer.RpcAddModifier<ElusiveShieldModifier>();
        OverrideName(TouLocale.Get("SuperSquadRoleElusiveShielding", "Shielding"));
    }

    /// <inheritdoc />
    public override void OnEffectEnd()
    {
        OverrideName(TouLocale.Get("SuperSquadRoleElusiveShield", "Shield"));
    }
}
