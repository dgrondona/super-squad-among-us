using MiraAPI.Modifiers;
using UnityEngine;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// Marks a player as bombed by the Detonator. Pure state, no visible indicator to the target or
/// bystanders (stealth, matching Witch's hex/Sniper's shot - no in-game tell besides the kill itself).
/// The explosion center is read live from <see cref="BaseModifier.Player"/>'s current position at
/// detonate time (see <see cref="SuperSquadAmongUs.Modules.SuperSquadDetonator.RpcDetonate"/>), so
/// unlike a stationary planted bomb this one always follows a moving target - nothing needs to track
/// position here. Removed (with no detonation) the instant a meeting is called - "bomb removed after a
/// meeting" from the modifier's own lifecycle, no separate event handler needed.
/// </summary>
public sealed class DetonatorBombModifier(PlayerControl detonator) : BaseModifier
{
    /// <summary>
    /// Gets the Detonator who planted this bomb.
    /// </summary>
    public PlayerControl Detonator { get; } = detonator;

    /// <summary>
    /// Gets the local time (<see cref="Time.time"/>) the bomb was planted, used for the client-side
    /// arm-delay gate on the Detonate button.
    /// </summary>
    public float PlantedAt { get; private set; }

    /// <inheritdoc />
    public override string ModifierName => "Bombed";

    /// <inheritdoc />
    public override bool HideOnUi => true;

    /// <inheritdoc />
    public override void OnActivate()
    {
        PlantedAt = Time.time;
    }

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
