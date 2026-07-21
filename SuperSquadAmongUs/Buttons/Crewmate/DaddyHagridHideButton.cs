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
/// Daddy Hagrid's Hide ability: tuck the nearest player into the cloak (see
/// <see cref="CloakHiddenModifier"/>). Deliberately NOT an <see cref="IKillButton"/> - hiding someone
/// is not an attack (though TOU-global rules still apply: clicking an alerted Veteran retaliates).
/// The button effect mirrors the hide duration, then the cooldown starts.
/// </summary>
public sealed class DaddyHagridHideButton : SuperSquadRoleButton<DaddyHagridRole, PlayerControl>
{
    /// <inheritdoc />
    public override string Name => TouLocale.GetParsed("SuperSquadRoleDaddyHagridHide", "Hide");

    /// <inheritdoc />
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;

    /// <inheritdoc />
    public override Color TextOutlineColor => SuperSquadColors.DaddyHagrid;

    /// <inheritdoc />
    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<DaddyHagridOptions>.Instance.HideCooldown + MapCooldown, 5f, 120f);

    /// <inheritdoc />
    public override float EffectDuration => OptionGroupSingleton<DaddyHagridOptions>.Instance.HideDuration;

    /// <inheritdoc />
    public override int MaxUses => (int)OptionGroupSingleton<DaddyHagridOptions>.Instance.MaxUses;

    /// <inheritdoc />
    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.CrewmatePlaceholderButton;

    /// <inheritdoc />
    protected override void OnClick()
    {
        if (Target == null)
        {
            return;
        }

        Target.RpcAddModifier<CloakHiddenModifier>(PlayerControl.LocalPlayer);
    }

    /// <inheritdoc />
    public override PlayerControl? GetTarget()
    {
        // Hagrid doesn't know teams: anyone living fits under the cloak, impostors included - but
        // not someone already carried (devoured or already hidden).
        return PlayerControl.LocalPlayer.GetClosestLivingPlayer(true, Distance, false,
            x => !x.HasModifier<CarriedModifier>());
    }
}
