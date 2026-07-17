using TownOfUs.Modifiers;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// Companion to <see cref="DevouredModifier"/>: plugs into TOU-Mira's <see cref="DisabledModifier"/>
/// checks so a devoured player can't report, use abilities/consoles, or open the map, and can't be
/// targeted by anyone's buttons or the vanilla kill button while in the stomach. Added/removed locally
/// by <see cref="DevouredModifier"/> on every client (that modifier is the synced one).
/// </summary>
public sealed class DevouredDisabledModifier : DisabledModifier
{
    /// <inheritdoc />
    public override string ModifierName => "Devoured (Incapacitated)";

    /// <inheritdoc />
    public override bool CanBeInteractedWith => false;

    /// <inheritdoc />
    public override bool CanUseAbilities => false;

    /// <inheritdoc />
    public override bool CanReport => false;

    /// <inheritdoc />
    public override bool HideOnUi => true;
}
