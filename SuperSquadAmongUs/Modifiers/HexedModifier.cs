using MiraAPI.Modifiers;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// Marks a player as hexed by the Witch. Pure state: the victim gets no in-game indication (TOR
/// convention - the meeting overlay in <c>Patches/WitchMeetingPatch.cs</c> is the only tell), and the
/// death itself is resolved at the next ejection by <c>Events/WitchEvents.cs</c>. Synced to all
/// clients via RpcAddModifier so every client can resolve the meeting deterministically.
/// </summary>
public sealed class HexedModifier(PlayerControl witch) : BaseModifier
{
    /// <summary>
    /// Gets the Witch who cast this hex.
    /// </summary>
    public PlayerControl Witch { get; } = witch;

    /// <inheritdoc />
    public override string ModifierName => "Hexed";

    /// <inheritdoc />
    public override bool HideOnUi => true;
}
