using MiraAPI.Modifiers;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// Dumper's held body: fully hidden (not visibly dragged along, unlike TOU-Mira's Undertaker), then
/// dropped and revealed at the Dumper's current position. Lives on the Dumper (the carrier), not the
/// body - <see cref="DeadBody"/> is a plain scene object, not a <see cref="PlayerControl"/>, so it can't
/// host a modifier itself the way <see cref="CarriedModifier"/>'s living-player payload can.
/// <para/>
/// Plain state, NOT a <c>TimedModifier</c>: the store-duration timing is owned by
/// <see cref="Buttons.Impostor.DumperCarryButton"/>, which auto-drops it (via <c>RpcRemoveModifier</c>)
/// on the Dumper's own client once the duration elapses - the same button-owns-its-timing pattern
/// RC-XD and the Detonator use, and deterministic across clients since one client drives the removal.
/// See docs/roles/dumper.md.
/// </summary>
public sealed class DumperCarryModifier(byte bodyId) : BaseModifier
{
    /// <summary>
    /// Gets the <see cref="DeadBody.ParentId"/> of the body currently held.
    /// </summary>
    public byte BodyId { get; } = bodyId;

    /// <inheritdoc />
    public override string ModifierName => "Carrying";

    /// <inheritdoc />
    public override bool HideOnUi => true;

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

        // The dead player's pet lingers at the death spot as its own object - hide/show it with the body.
        // petHiddenByViper is the key: a bare TogglePet(false) gets undone when the CARRIER exits a vent
        // (vanilla re-shows pets on vent exit), which is why a body dropped from a vent used to leave the
        // pet visible. Setting the flag (MiscUtils.RemovePet's PetHidden.DuringRound mechanism) suppresses
        // that automatic re-show; clear it again when revealing on drop.
        var owner = MiscUtils.PlayerById(body.ParentId);
        if (owner != null && owner.cosmetics != null)
        {
            owner.cosmetics.petHiddenByViper = !visible;
            owner.cosmetics.TogglePet(visible);
        }
    }
}
