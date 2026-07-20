using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Modifiers.Types;
using SuperSquadAmongUs.Options.Roles.Impostor;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// Dumper's held body: fully hidden (not visibly dragged along, unlike TOU-Mira's Undertaker) for a
/// timed duration, then auto-dropped and revealed at the Dumper's current position. Lives on the
/// Dumper (the carrier), not the body - <see cref="DeadBody"/> is a plain scene object, not a
/// <see cref="PlayerControl"/>, so it can't host a modifier itself the way <see cref="CarriedModifier"/>'s
/// living-player payload can. See docs/roles/dumper.md.
/// </summary>
public sealed class DumperCarryModifier(byte bodyId) : TimedModifier
{
    /// <summary>
    /// Gets the <see cref="DeadBody.ParentId"/> of the body currently held.
    /// </summary>
    public byte BodyId { get; } = bodyId;

    /// <inheritdoc />
    public override string ModifierName => "Carrying";

    /// <inheritdoc />
    public override bool HideOnUi => true;

    /// <inheritdoc />
    public override float Duration => OptionGroupSingleton<DumperOptions>.Instance.CarryDuration;

    /// <inheritdoc />
    public override bool AutoStart => true;

    private DeadBody? FindBody()
    {
        foreach (var body in UnityEngine.Object.FindObjectsOfType<DeadBody>())
        {
            if (body != null && body.ParentId == BodyId)
            {
                return body;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public override void OnActivate()
    {
        var body = FindBody();
        if (body == null)
        {
            // The body vanished between the pickup click and this modifier arriving (cleaned,
            // reported-and-removed, etc.) - nothing to hide, so there's nothing to carry either.
            Player.RemoveModifier(this);
            return;
        }

        SetBodyVisible(body, false);
    }

    /// <inheritdoc />
    public override void OnDeactivate()
    {
        var body = FindBody();
        if (body == null)
        {
            return;
        }

        var dropPos = Player.transform.position;
        dropPos.z = dropPos.y / 1000f;
        body.transform.position = dropPos;

        SetBodyVisible(body, true);
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

    /// <inheritdoc />
    public override void FixedUpdate()
    {
        // Advances the TimedModifier's auto-drop timer - must run.
        base.FixedUpdate();

        // The dead player's pet is a separate object the game can re-show on its own; keep it hidden
        // every tick while the body is carried so nothing is left visible on the ground.
        var owner = MiscUtils.PlayerById(BodyId);
        if (owner != null && owner.cosmetics != null && owner.cosmetics.currentPet != null &&
            owner.cosmetics.currentPet.gameObject.activeSelf)
        {
            owner.cosmetics.TogglePet(false);
        }
    }

    private static void SetBodyVisible(DeadBody body, bool visible)
    {
        var alpha = visible ? 1f : 0f;
        foreach (var renderer in body.bodyRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            var color = renderer.color;
            renderer.color = new Color(color.r, color.g, color.b, alpha);
        }

        // Stops the body from being reportable/clickable while carried, matching TOU-Mira's own
        // body-hide technique (Janitor's clean fade, TownOfUs/Utilities/Extensions.cs CoCleanCustom).
        body.myCollider.enabled = visible;

        // The dead player's pet lingers at the death spot as its own object - hide/show it with the body
        // so a carried body leaves nothing behind (MiscUtils.RemovePet's TogglePet toggle).
        var owner = MiscUtils.PlayerById(body.ParentId);
        if (owner != null && owner.cosmetics != null)
        {
            owner.cosmetics.TogglePet(visible);
        }
    }
}
