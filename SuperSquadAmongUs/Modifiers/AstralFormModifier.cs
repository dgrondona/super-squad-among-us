using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using SuperSquadAmongUs.Options.Roles.Impostor;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// The Astral's ghost phase: invisible (via <see cref="AstralInvisibilityModifier"/>) and able to walk
/// through walls (collider disabled). When the duration expires the player snaps back to where they
/// phased from and, if configured, lingers invisible via <see cref="AstralLingerModifier"/>.
/// </summary>
public sealed class AstralFormModifier : AstralInvisibilityModifier
{
    private Vector2 returnPosition;

    /// <inheritdoc />
    public override string ModifierName => "Astral Form";

    /// <inheritdoc />
    public override float Duration => OptionGroupSingleton<AstralOptions>.Instance.FormDuration;

    /// <inheritdoc />
    public override void OnActivate()
    {
        returnPosition = Player.transform.position;
        Player.Collider.enabled = false;
        base.OnActivate();
    }

    /// <inheritdoc />
    public override void OnDeactivate()
    {
        Player.Collider.enabled = true;
        base.OnDeactivate();

        // The snap-back is client-authoritative movement, so only the owner performs it (RpcSnapTo
        // syncs it). Skipped when the phase ended because of a meeting or death rather than timeout.
        if (!Player.AmOwner || Player.HasDied() || MeetingHud.Instance)
        {
            return;
        }

        Player.NetTransform.RpcSnapTo(returnPosition);

        if (OptionGroupSingleton<AstralOptions>.Instance.LingerDuration > 0f)
        {
            Player.RpcAddModifier<AstralLingerModifier>();
        }
    }

    /// <inheritdoc />
    public override string GetDescription()
    {
        return "You are a spirit: unseen, walking through walls!";
    }
}
