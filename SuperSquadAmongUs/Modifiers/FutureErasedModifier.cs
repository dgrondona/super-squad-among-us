using MiraAPI.Modifiers;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// Marks a player whose modded role will be stripped at the next meeting's exile screen (the
/// Eraser's TOR mechanic - see Events/EraserEvents.cs for the resolution). Pure state, synced via
/// RpcAddModifier like <see cref="HexedModifier"/>. The target gets no notification (user decision,
/// 2026-07-16) - they'll notice their role UI is gone.
/// </summary>
public sealed class FutureErasedModifier(PlayerControl eraser) : BaseModifier
{
    /// <summary>
    /// Gets the Eraser who marked this player.
    /// </summary>
    public PlayerControl Eraser { get; } = eraser;

    /// <inheritdoc />
    public override string ModifierName => "Erased";

    /// <inheritdoc />
    public override bool HideOnUi => true;
}
