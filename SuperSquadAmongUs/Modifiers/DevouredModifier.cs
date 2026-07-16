using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Utilities;
using TownOfUs.Modifiers;
using TownOfUs.Options;
using TownOfUs.Utilities;
using TownOfUs.Utilities.Appearances;
using UnityEngine;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// The "in the Pelican's stomach" state: the player is alive but hidden from everyone, frozen, and
/// pinned to the Pelican's position every physics tick (so their camera effectively spectates the
/// Pelican). Removed with a release when the Pelican is killed mid-round; devoured players die at the
/// start of the next meeting (see Events/PelicanEvents.cs). Indefinite: inherits ConcealedModifier's
/// AutoStart=false so the timer never runs.
/// </summary>
public sealed class DevouredModifier(PlayerControl pelican) : ConcealedModifier, IVisualAppearance
{
    /// <summary>
    /// Gets the Pelican that devoured this player.
    /// </summary>
    public PlayerControl Pelican { get; } = pelican;

    /// <inheritdoc />
    public override string ModifierName => "Devoured";

    /// <inheritdoc />
    public override bool VisibleToOthers => false;

    /// <inheritdoc />
    public bool VisualPriority => true;

    /// <summary>
    /// Builds the blanked-out appearance used while devoured. Unlike the invisibility roles, nobody
    /// gets an outline except the devoured player themself (and the informed dead) - fellow players
    /// are not supposed to know who's in the stomach.
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
        Player.AddModifier<DevouredDisabledModifier>();

        // Freeze movement the way Ambusher does; the per-tick pin below is the only position source
        // while devoured (the paused NetTransform stops the owner broadcasting competing positions).
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

        // Pin to the Pelican every tick: the devoured player rides along in the stomach.
        if (Pelican != null && !Pelican.HasDied())
        {
            Player.MyPhysics.body.position = Pelican.MyPhysics.body.position;
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

        if (Player.HasModifier<DevouredDisabledModifier>())
        {
            Player.RemoveModifier<DevouredDisabledModifier>();
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

    /// <inheritdoc />
    public override string GetDescription()
    {
        return "You have been devoured by the Pelican!";
    }

    private bool LocalViewerSeesOutline()
    {
        return Player.AmOwner ||
               (PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.DiedOtherRound() &&
                OptionGroupSingleton<GeneralOptions>.Instance.TheDeadKnow);
    }

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
