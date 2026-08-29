using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Keybinds;
using MiraAPI.Modifiers;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Modules;
using SuperSquadAmongUs.Options.Modifiers;
using TownOfUs.Buttons;
using TownOfUs.Modules.Localization;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Modifiers;

/// <summary>
/// Ability button granted by the <see cref="InvisibilityCloakModifier"/>: throw on the cloak to become
/// invisible (via <see cref="CloakInvisibilityModifier"/>) for the configured duration. Lives in the
/// bottom-left modifier-button slot like TOU-Mira's Button Barry.
/// </summary>
public sealed class InvisibilityCloakButton : TownOfUsButton
{
    /// <inheritdoc />
    public override string Name => TouLocale.GetParsed("SuperSquadModifierInvisibilityCloakButton", "Cloak");

    /// <inheritdoc />
    public override BaseKeybind Keybind => Keybinds.ModifierAction;

    /// <inheritdoc />
    public override Color TextOutlineColor => SuperSquadColors.InvisibilityCloak;

    /// <inheritdoc />
    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<InvisibilityCloakOptions>.Instance.Cooldown + MapCooldown, 5f, 120f);

    /// <inheritdoc />
    public override float EffectDuration => OptionGroupSingleton<InvisibilityCloakOptions>.Instance.Duration;

    /// <inheritdoc />
    public override ButtonLocation Location => ButtonLocation.BottomLeft;

    /// <inheritdoc />
    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.NeutralPlaceholderButton;

    /// <inheritdoc />
    public override bool Enabled(RoleBehaviour? role)
    {
        return PlayerControl.LocalPlayer &&
               PlayerControl.LocalPlayer.HasModifier<InvisibilityCloakModifier>() &&
               !PlayerControl.LocalPlayer.Data.IsDead;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Arbitrates shared keybinds - see <see cref="KeybindArbiter"/>. Claims only after
    /// <c>CanClick()</c> passes, so a button that can't actually fire (cooldown, no target) doesn't
    /// consume the keypress and block a ready sibling on the same keybind.
    /// </remarks>
    public override void ClickHandler()
    {
        if (!CanClick() || !KeybindArbiter.TryClaim(Keybind))
        {
            return;
        }

        base.ClickHandler();
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        PlayerControl.LocalPlayer.RpcAddModifier<CloakInvisibilityModifier>();
    }

    /// <inheritdoc />
    public override void OnEffectEnd()
    {
        // The modifier's own timer removes it in the normal case (both run the same duration); this
        // guard only covers drift between the button timer and the modifier timer.
        if (PlayerControl.LocalPlayer.HasModifier<CloakInvisibilityModifier>())
        {
            PlayerControl.LocalPlayer.RpcRemoveModifier<CloakInvisibilityModifier>();
        }
    }
}
