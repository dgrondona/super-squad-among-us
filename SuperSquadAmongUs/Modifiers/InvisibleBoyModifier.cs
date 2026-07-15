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
        // Unlike Swooper (impostor-aligned teammates see the ghostly outline), Invisible Boy is a
        // crewmate - only he himself sees his own outline while alive, plus the dead-who-know crowd.
        var playerColor = (Player.AmOwner || (PlayerControl.LocalPlayer.DiedOtherRound() &&
                                                OptionGroupSingleton<GeneralOptions>.Instance.TheDeadKnow))
            ? new Color(0f, 0f, 0f, 0.1f)
            : Color.clear;

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
    }

    /// <inheritdoc />
    public override void FixedUpdate()
    {
        base.FixedUpdate();

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
}
