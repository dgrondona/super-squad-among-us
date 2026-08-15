using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.PluginLoading;
using MiraAPI.Utilities;
using TownOfUs.Modifiers;
using TownOfUs.Options;
using TownOfUs.Utilities;
using TownOfUs.Utilities.Appearances;
using UnityEngine;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// Shared "carried by another player" state: the player is alive but hidden from everyone, frozen, and
/// pinned to the carrier's position every physics tick (so their camera effectively spectates the
/// carrier). Extracted from the Pelican's devour logic so the Daddy Hagrid's cloak can reuse it; see
/// <see cref="DevouredModifier"/> and <see cref="CloakHiddenModifier"/>. Subclasses stay sealed
/// siblings - event handlers iterate concrete types and must never catch each other's carried players.
/// Inherits ConcealedModifier's AutoStart=false, so the state is indefinite unless a subclass opts
/// into a timer.
/// </summary>
[MiraIgnore]
public abstract class CarriedModifier(PlayerControl carrier) : ConcealedModifier, IVisualAppearance
{
    /// <summary>
    /// Gets the player carrying this one (the Pelican that devoured them, the Hagrid hiding them).
    /// </summary>
    public PlayerControl Carrier { get; } = carrier;

    /// <inheritdoc />
    public override bool VisibleToOthers => false;

    /// <inheritdoc />
    public bool VisualPriority => true;

    /// <summary>
    /// Builds the blanked-out appearance used while carried. Unlike the invisibility roles, nobody
    /// gets an outline except the carried player themself (and the informed dead) - fellow players
    /// are not supposed to know who's being carried.
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
        Player.RawSetAppearance(this);
        Player.cosmetics.ToggleNameVisible(false);

        // Blocks report/abilities/consoles/map and makes the player untargetable, via TOU-Mira's
        // DisabledModifier checks. Added locally on every client - this modifier is the synced one.
        Player.AddModifier<CarriedDisabledModifier>();

        // Freeze movement the way Ambusher does; the per-tick pin below is the only position source
        // while carried (the paused NetTransform stops the owner broadcasting competing positions).
        if (Player.AmOwner)
        {
            Player.moveable = false;
            Player.MyPhysics.ResetMoveState();
            Player.NetTransform.SetPaused(true);
        }
    }

    /// <inheritdoc />
    public override void FixedUpdate()
    {
        base.FixedUpdate();

        // Pin to the carrier every tick: the carried player rides along.
        if (Carrier != null && !Carrier.HasDied())
        {
            Player.MyPhysics.body.position = Carrier.MyPhysics.body.position;
        }

        ApplyLocalVisibility();

        // Self-heal: if anything reset this player's appearance, re-blank it within a tick.
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

        if (Player.HasModifier<CarriedDisabledModifier>())
        {
            Player.RemoveModifier<CarriedDisabledModifier>();
        }

        if (Player.AmOwner)
        {
            Player.moveable = true;
            Player.NetTransform.SetPaused(false);
        }
        else
        {
            Player.Visible = true;
        }
    }

    /// <inheritdoc />
    public override void OnDeath(DeathReason reason)
    {
        Player.RemoveModifier(this);
    }

    /// <summary>
    /// Whether the local viewer should see the faint outline rather than nothing: the carried player
    /// themself, or a dead player when "the dead know" is on.
    /// </summary>
    /// <returns>True if the local viewer sees the faint outline.</returns>
    protected bool LocalViewerSeesOutline()
    {
        return Player.AmOwner ||
               (PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.DiedOtherRound() &&
                OptionGroupSingleton<GeneralOptions>.Instance.TheDeadKnow);
    }

    /// <summary>
    /// Fully removes the player from this client's rendering (main view AND cameras) unless the local
    /// viewer is meant to see the outline. Never touches the owner's own Visible.
    /// </summary>
    protected void ApplyLocalVisibility()
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
