using MiraAPI.Hud;
using SuperSquadAmongUs.Buttons.Neutral;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// Kirby's granted Swoop concealment, inherited by digesting a Swooper. See
/// <see cref="GrantedSwoopModifierBase"/> for the shared shape.
/// </summary>
public sealed class KirbySwoopModifier : GrantedSwoopModifierBase
{
    /// <inheritdoc />
    protected override void UpdateButtonVisual(bool swooped)
    {
        var button = CustomButtonSingleton<KirbySwoopButton>.Instance;
        button.OverrideName(swooped
            ? TouLocale.GetParsed("SuperSquadRoleKirbyUnswoop", "Unswoop")
            : TouLocale.GetParsed("SuperSquadRoleKirbySwoop", "Swoop"));
    }
}
