using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using TownOfUs.Modifiers;
using TownOfUs.Modifiers.Impostor;
using TownOfUs.Options;
using TownOfUs.Patches;
using TownOfUs.Utilities;
using TownOfUs.Utilities.Appearances;
using UnityEngine;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// Makes Invisible Boy invisible to everyone while active. Added/removed by the Invisible Boy role
/// whenever no living player can currently see him; never auto-expires (inherits
/// <see cref="ConcealedModifier"/>'s <c>AutoStart => false</c>, so the timer never starts).
/// </summary>
public sealed class InvisibleBoyModifier : ConcealedModifier, IVisualAppearance
{
    // The pinned TownOfUsMira package predates TOU-Mira's VanillaSystemCheckPatches cache, so this
    // caches the vanilla mushroom-mixup system the same way that class does (FindObjectOfType at use
    // time), fetched once per activation.
    private MushroomMixupSabotageSystem? shroomSystem;

    /// <inheritdoc />
    public override string ModifierName => "Invisible";

    /// <inheritdoc />
    public override bool VisibleToOthers => false;

    /// <inheritdoc />
    public bool VisualPriority => true;

    /// <summary>
    /// Builds the blanked-out appearance (no hat/skin/visor/pet/name) used while invisible.
    /// </summary>
    /// <remarks>
    /// Reuses <see cref="TownOfUsAppearances.Swooper"/> as the appearance type rather than adding a new
    /// one - this piggybacks on TOU-Mira's existing comms-camouflage skip for that appearance so
    /// Invisible Boy stays hidden during comms sabotage too.
    /// </remarks>
    /// <returns>The <see cref="VisualAppearance"/> to render for this player.</returns>
    public VisualAppearance GetVisualAppearance()
    {
        var playerColor = LocalViewerSeesOutline() ? new Color(0f, 0f, 0f, 0.1f) : Color.clear;

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
        shroomSystem = UnityEngine.Object.FindObjectOfType<MushroomMixupSabotageSystem>();
        Player.RawSetAppearance(this);
        Player.cosmetics.ToggleNameVisible(false);
        ApplyLocalVisibility();
    }

    /// <inheritdoc />
    public override void FixedUpdate()
    {
        base.FixedUpdate();

        // Re-assert every tick: the transparent appearance only hides the main view, but security
        // cameras (and TOU-Mira's IsVisibleToOthers) honor PlayerControl.Visible, not sprite alpha, and
        // vanilla flips Visible back on across vent/ladder animations. See docs/roles/invisible-boy.md.
        ApplyLocalVisibility();

        if (shroomSystem && shroomSystem!.IsActive)
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

        // Only the viewers we hid need restoring; the owner's Visible was never touched.
        if (!Player.AmOwner)
        {
            Player.Visible = true;
        }

        if (HudManagerPatches.CamouflageCommsEnabled)
        {
            Player.cosmetics.ToggleNameVisible(false);
        }

        if (shroomSystem && shroomSystem!.IsActive)
        {
            SwoopModifier.MushroomMixUp(shroomSystem, Player);
        }
    }

    /// <inheritdoc />
    public override void OnDeath(DeathReason reason)
    {
        Player.RemoveModifier(this);
    }

    /// <summary>
    /// Forces Invisible Boy visible for the duration of meetings; the role's watcher loop re-adds this
    /// modifier after the meeting ends if he's still unseen.
    /// </summary>
    public override void OnMeetingStart()
    {
        Player.RemoveModifier(this);
    }

    /// <inheritdoc />
    public override string GetDescription()
    {
        return "You are invisible while nobody watches!";
    }

    // Whether the local viewer is one who should still see the ghostly outline rather than nothing:
    // the Invisible Boy himself, or a dead player when "the dead know" is on (Swooper convention).
    private bool LocalViewerSeesOutline()
    {
        return Player.AmOwner ||
               (PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.DiedOtherRound() &&
                OptionGroupSingleton<GeneralOptions>.Instance.TheDeadKnow);
    }

    // Fully removes the player from this client's rendering (main view AND cameras) unless the local
    // viewer is meant to see the outline. Never touches the owner's own Visible - his outline shows via
    // the appearance alpha and his vent/ladder visibility stays vanilla-managed. Visible is a local,
    // per-client rendering flag, so hiding him here doesn't affect his own screen.
    private void ApplyLocalVisibility()
    {
        if (Player.AmOwner)
        {
            return;
        }

        var shouldBeVisible = LocalViewerSeesOutline();
        if (Player.Visible != shouldBeVisible)
        {
            Player.Visible = shouldBeVisible;
        }
    }
}
