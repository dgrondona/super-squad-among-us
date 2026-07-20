using MiraAPI.Modifiers;
using TownOfUs.Modifiers;
using TownOfUs.Utilities;
using TownOfUs.Utilities.Appearances;
using UnityEngine;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// Shared shape for the Swoop pool ability's self-concealment modifier (Gooper/Kirby). Copies
/// TOU-Mira's <c>SwoopModifier</c> shape rather than subclassing it directly: that class hard-codes
/// <c>CustomButtonSingleton&lt;SwooperSwoopButton&gt;</c> calls in <c>OnActivate</c>/<c>OnDeactivate</c>
/// to flip the Swooper's own button sprite, and reusing it as-is would cross-wire into the actual
/// Swooper role. <see cref="UpdateButtonVisual"/> is the per-role hook a sealed sibling implements
/// instead, against its own button singleton. See <see cref="Buttons.GrantedSwoopButtonBase{TRole,TModifier}"/>.
/// </summary>
public abstract class GrantedSwoopModifierBase : ConcealedModifier, IVisualAppearance
{
    /// <inheritdoc />
    public override string ModifierName => "Swooped";

    /// <inheritdoc />
    public override bool HideOnUi => true;

    // Deliberately NOT AutoStart: ConcealedModifier.Duration defaults to 1f, so an auto-started timer
    // would silently drop the concealment after one second. The granting button owns the timing via its
    // EffectDuration and removes this modifier in OnEffectEnd (see GrantedSwoopButtonBase), the same way
    // TOU-Mira's SwoopModifier is driven by SwooperSwoopButton's effect window.
    /// <inheritdoc />
    public override bool AutoStart => false;

    /// <inheritdoc />
    public override bool VisibleToOthers => false;

    /// <inheritdoc />
    public bool VisualPriority => true;

    /// <summary>
    /// Flips the granting button's sprite/name to reflect swoop state. Implemented per role since each
    /// grants this ability through its own button singleton, not the Swooper's.
    /// </summary>
    /// <param name="swooped">True when the modifier just activated, false when it just deactivated.</param>
    protected abstract void UpdateButtonVisual(bool swooped);

    /// <inheritdoc />
    public VisualAppearance GetVisualAppearance()
    {
        // Owner sees themself faintly (so a neutral swooper isn't fully invisible to their own camera);
        // impostor-aligned viewers likewise; everyone else sees nothing.
        var seesOutline = Player.AmOwner || PlayerControl.LocalPlayer.IsImpostorAligned();
        var playerColor = seesOutline ? new Color(0f, 0f, 0f, 0.1f) : Color.clear;

        return new VisualAppearance(Player.GetDefaultModifiedAppearance(), TownOfUsAppearances.Swooper)
        {
            HatId = "hat_NoHat",
            SkinId = "skin_None",
            VisorId = "visor_EmptyVisor",
            PlayerName = string.Empty,
            PetId = "pet_EmptyPet",
            RendererColor = playerColor,
            NameColor = Color.clear,
            ColorBlindTextColor = Color.clear,
        };
    }

    /// <inheritdoc />
    public override void OnActivate()
    {
        Player.RawSetAppearance(this);
        Player.cosmetics.ToggleNameVisible(false);

        if (Player.AmOwner)
        {
            UpdateButtonVisual(true);
        }
    }

    /// <inheritdoc />
    public override void FixedUpdate()
    {
        base.FixedUpdate();

        // Self-heal each tick (house doctrine, docs/il2cpp-gotchas.md): vanilla animations can flip
        // appearance back mid-effect.
        if (Player.GetAppearanceType() != TownOfUsAppearances.Swooper)
        {
            Player.RawSetAppearance(this);
            Player.cosmetics.ToggleNameVisible(false);
        }
    }

    /// <inheritdoc />
    public override void OnDeactivate()
    {
        Player.ResetAppearance();
        Player.cosmetics.ToggleNameVisible(true);

        if (Player.AmOwner)
        {
            UpdateButtonVisual(false);
        }
    }

    /// <inheritdoc />
    public override void OnMeetingStart()
    {
        Player.RemoveModifier(this);
    }
}
