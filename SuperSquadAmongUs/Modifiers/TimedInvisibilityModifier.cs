using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Utilities;
using TownOfUs.Modifiers;
using TownOfUs.Modifiers.Impostor;
using TownOfUs.Options;
using TownOfUs.Patches;
using TownOfUs.Utilities;
using TownOfUs.Utilities.Appearances;
using UnityEngine;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// Shared invisibility rendering for the Astral's two phases (<see cref="AstralFormModifier"/> and
/// <see cref="AstralLingerModifier"/>). Same appearance approach as <see cref="InvisibleBoyModifier"/>,
/// but timed (auto-starts) and with Swooper's viewer rule: fellow impostors and the informed dead see a
/// faint outline, everyone else sees nothing.
/// </summary>
public abstract class TimedInvisibilityModifier : ConcealedModifier, IVisualAppearance
{
    // See InvisibleBoyModifier: the pinned TownOfUsMira package predates VanillaSystemCheckPatches'
    // cache, so fetch the mushroom-mixup system once per activation the same way.
    private MushroomMixupSabotageSystem? shroomSystem;

    /// <inheritdoc />
    public override bool AutoStart => true;

    /// <inheritdoc />
    public override bool VisibleToOthers => false;

    /// <inheritdoc />
    public bool VisualPriority => true;

    /// <summary>
    /// Builds the blanked-out appearance (no hat/skin/visor/pet/name) used while phased.
    /// </summary>
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

        // Re-assert every tick: cameras and TOU-Mira's IsVisibleToOthers honor PlayerControl.Visible,
        // not sprite alpha, and vanilla flips Visible back on across vent/ladder animations. See
        // docs/roles/invisible-boy.md for the full story.
        ApplyLocalVisibility();

        // Self-heal the appearance too: if anything reset this player to their normal look (e.g. the
        // form->linger handoff, where the ending phase's ResetAppearance can land after the next
        // phase's RawSetAppearance), re-blank it within a tick.
        if (Player.GetAppearanceType() != TownOfUsAppearances.Swooper)
        {
            Player.RawSetAppearance(this);
            Player.cosmetics.ToggleNameVisible(false);
        }

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

    /// <summary>
    /// Ends the phase early when a meeting is called; the phase does not resume afterwards.
    /// </summary>
    public override void OnMeetingStart()
    {
        Player.RemoveModifier(this);
    }

    /// <summary>
    /// Whether the local viewer should see the faint outline rather than nothing: the phased player,
    /// a fellow impostor, or a dead player when "the dead know" is on (Swooper convention). Virtual so
    /// non-impostor users (e.g. <see cref="CloakInvisibilityModifier"/>) can drop the impostor clause.
    /// </summary>
    /// <returns>True if the local viewer sees the faint outline.</returns>
    protected virtual bool LocalViewerSeesOutline()
    {
        return Player.AmOwner ||
               (PlayerControl.LocalPlayer &&
                (PlayerControl.LocalPlayer.IsImpostorAligned() ||
                 (PlayerControl.LocalPlayer.DiedOtherRound() &&
                  OptionGroupSingleton<GeneralOptions>.Instance.TheDeadKnow)));
    }

    // Fully removes the player from this client's rendering (main view AND cameras) unless the local
    // viewer is meant to see the outline. Never touches the owner's own Visible. See
    // InvisibleBoyModifier.ApplyLocalVisibility for why.
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
