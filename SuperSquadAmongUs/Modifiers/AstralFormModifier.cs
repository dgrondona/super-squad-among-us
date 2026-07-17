using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Networking;
using SuperSquadAmongUs.Options.Roles.Impostor;
using TownOfUs.Events;
using TownOfUs.Modifiers;
using TownOfUs.Modules.Localization;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// The Astral's ghost phase: invisible (via <see cref="TimedInvisibilityModifier"/>) and able to walk
/// through walls (collider disabled). When the duration expires the player snaps back to where they
/// phased from and, if configured, lingers invisible via <see cref="AstralLingerModifier"/>. If
/// <see cref="AstralOptions.DieWithoutKill"/> is on and the Astral didn't kill anyone this phase, they
/// die instead of lingering.
/// </summary>
public sealed class AstralFormModifier : TimedInvisibilityModifier
{
    private Vector2 returnPosition;

    /// <inheritdoc />
    public override string ModifierName => "Astral Form";

    /// <inheritdoc />
    public override float Duration => OptionGroupSingleton<AstralOptions>.Instance.FormDuration;

    /// <summary>
    /// Whether the Astral killed anyone during this phase, tracked by <see cref="Events.AstralEvents"/>.
    /// Only consulted when <see cref="AstralOptions.DieWithoutKill"/> is on.
    /// </summary>
    public bool HasKilled { get; set; }

    /// <inheritdoc />
    public override void OnActivate()
    {
        returnPosition = Player.transform.position;
        Player.Collider.enabled = false;
        base.OnActivate();
    }

    /// <inheritdoc />
    public override void FixedUpdate()
    {
        base.FixedUpdate();

        // Self-heal like the appearance above: the kill animation (KillAnimation.SetMovement et al.)
        // re-enables the collider mid-phase, which used to end the wall-phase early on a kill. Keep it
        // off for the whole phase instead of only disabling it once in OnActivate.
        if (Player.Collider.enabled)
        {
            Player.Collider.enabled = false;
        }
    }

    /// <inheritdoc />
    public override void OnDeactivate()
    {
        Player.Collider.enabled = true;
        base.OnDeactivate();

        // Skipped when the phase ended because of a meeting or death rather than timeout.
        if (Player.HasDied() || MeetingHud.Instance)
        {
            return;
        }

        // The snap-back is client-authoritative movement, so only the owner performs it (RpcSnapTo
        // syncs it to everyone else). Done before the kill/linger branch so a self-kill's body doesn't
        // spawn wherever the phase happened to end (potentially inside a wall).
        if (Player.AmOwner)
        {
            Player.NetTransform.RpcSnapTo(returnPosition);
        }

        if (!HasKilled && OptionGroupSingleton<AstralOptions>.Instance.DieWithoutKill)
        {
            DieFromFailedReturn();
            return;
        }

        // Every client adds the linger locally, in the same tick the form ends. An RPC here would open
        // a latency window where remote clients render the astral fully visible between the phases
        // (each client's form timer expires on its own; see TimedModifier.FixedUpdate).
        if (OptionGroupSingleton<AstralOptions>.Instance.LingerDuration > 0f)
        {
            Player.AddModifier<AstralLingerModifier>();
        }
    }

    /// <inheritdoc />
    public override string GetDescription()
    {
        return "You are a spirit: unseen, walking through walls!";
    }

    // Runs identically on every client: each client's form timer expires on its own tick (see
    // TimedModifier.FixedUpdate), and HasKilled is synced beforehand via AfterMurderEvent, which fires
    // on all clients too - so everyone reaches this branch in agreement, the same way LoverEvents
    // resolves a heartbreak death locally instead of over RPC.
    private void DieFromFailedReturn()
    {
        var showAnim = !MeetingHud.Instance && !ExileController.Instance;
        DeathHandlerModifier.UpdateDeathHandlerImmediate(
            Player,
            TouLocale.Get("SuperSquadDiedToAstral", "Faded"),
            DeathEventHandlers.CurrentRound,
            showAnim ? DeathHandlerOverride.SetTrue : DeathHandlerOverride.SetFalse,
            lockInfo: DeathHandlerOverride.SetTrue);

        Player.CustomMurder(
            Player,
            MurderResultFlags.DecisionByHost | MurderResultFlags.Succeeded,
            false,
            showAnim,
            false,
            showAnim,
            false);
    }
}
