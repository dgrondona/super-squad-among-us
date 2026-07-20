using MiraAPI.Hud;
using SuperSquadAmongUs.Buttons.Neutral;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// Gooper's granted Swoop concealment, one of the two v1 pool abilities (3rd goop and beyond). See
/// <see cref="GrantedSwoopModifierBase"/> for the shared shape.
/// </summary>
public sealed class GooperSwoopModifier : GrantedSwoopModifierBase
{
    /// <inheritdoc />
    protected override void UpdateButtonVisual(bool swooped)
    {
        var button = CustomButtonSingleton<GooperSwoopButton>.Instance;
        button.OverrideName(swooped
            ? TouLocale.GetParsed("SuperSquadRoleGooperUnswoop", "Unswoop")
            : TouLocale.GetParsed("SuperSquadRoleGooperSwoop", "Swoop"));
    }
}
