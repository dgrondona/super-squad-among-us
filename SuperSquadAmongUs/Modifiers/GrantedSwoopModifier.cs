using MiraAPI.Hud;
using MiraAPI.Modifiers;
using SuperSquadAmongUs.Buttons;
using TownOfUs.Modifiers;
using TownOfUs.Modules.Localization;
using TownOfUs.Utilities;
using TownOfUs.Utilities.Appearances;
using UnityEngine;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// The Swoop pool ability's self-concealment - the Swooper's own effect, but a sealed sibling of
/// TOU-Mira's <c>SwoopModifier</c> rather than that class directly, since it hard-codes
/// <c>CustomButtonSingleton&lt;SwooperSwoopButton&gt;</c> in <c>OnActivate</c>/<c>OnDeactivate</c> and
/// reusing it would cross-wire the actual Swooper role. A single role-agnostic modifier (paired with the
/// single <see cref="GrantedSwoopButton"/>) - no per-granting-role variants.
/// </summary>
public sealed class GrantedSwoopModifier : ConcealedModifier, IVisualAppearance
{
    /// <inheritdoc />
    public override string ModifierName => "Swooped";

    /// <inheritdoc />
    public override bool HideOnUi => true;

    // Deliberately NOT AutoStart: ConcealedModifier.Duration defaults to 1f, so an auto-started timer
    // would drop the concealment after one second. GrantedSwoopButton owns the timing via its
    // EffectDuration and removes this in OnEffectEnd, the way SwooperSwoopButton drives SwoopModifier.
    /// <inheritdoc />
    public override bool AutoStart => false;

    /// <inheritdoc />
    public override bool VisibleToOthers => false;

    /// <inheritdoc />
    public bool VisualPriority => true;

    /// <inheritdoc />
    public VisualAppearance GetVisualAppearance()
    {
        // The owner sees themself faintly, so a neutral swooper isn't fully invisible to their own
        // camera. Impostor-aligned viewers see the same faint outline, and everyone else sees nothing.
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

    private static void UpdateButtonVisual(bool swooped)
    {
        CustomButtonSingleton<GrantedSwoopButton>.Instance.OverrideName(swooped
            ? TouLocale.GetParsed("SuperSquadRoleGrantedUnswoop", "Unswoop")
            : TouLocale.GetParsed("SuperSquadRoleGrantedSwoop", "Swoop"));
    }
}
