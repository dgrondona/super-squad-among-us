using MiraAPI.Modifiers;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// Marks a player as protected by the Sui. Pure state (<c>HexedModifier</c>'s shape) - no visible
/// indicator to the protected player or anyone else. Read from the attacker's own client by
/// <see cref="Events.SuiEvents"/> to detect "someone interacted with my protected target"; unlike a
/// shield, this never cancels the interaction itself, it only arms Sui's retaliation. Removed (without
/// any release effect, since it isn't incapacitating anyone) at a meeting or the target's death.
/// </summary>
public sealed class SuiProtectedModifier(PlayerControl sui) : BaseModifier
{
    /// <summary>
    /// Gets the Sui protecting this player.
    /// </summary>
    public PlayerControl Sui { get; } = sui;

    /// <inheritdoc />
    public override string ModifierName => "Protected";

    /// <inheritdoc />
    public override bool HideOnUi => true;

    /// <inheritdoc />
    public override void OnMeetingStart()
    {
        Player.RemoveModifier(this);
    }

    /// <inheritdoc />
    public override void OnDeath(DeathReason reason)
    {
        Player.RemoveModifier(this);
    }
}
