using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Modifiers;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Options.Roles.Impostor;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs;
using TownOfUs.Buttons;
using TownOfUs.Modules.Localization;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Impostor;

/// <summary>
/// The Witch's hex: a short channeled cast on a nearby target. The channel cancels if the closest
/// target changes mid-cast (TOR behavior). Each successful cast cumulatively adds the "additional
/// cooldown" to future hexes, and optionally also puts the vanilla kill button on cooldown.
/// </summary>
public sealed class WitchHexButton : SuperSquadRoleButton<WitchRole, PlayerControl>
{
    private PlayerControl? castTarget;
    private PlayerControl? cachedTarget;
    private bool targetCached;

    public override string Name => TouLocale.GetParsed("SuperSquadRoleWitchHex", "Hex");
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => TownOfUsColors.Impostor;
    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<WitchOptions>.Instance.HexCooldown + CurrentCooldownAddition + MapCooldown, 5f, 240f);
    public override float EffectDuration => OptionGroupSingleton<WitchOptions>.Instance.CastDuration;
    public override LoadableAsset<Sprite> Sprite => SuperSquadImpAssets.WitchHexSprite;

    /// <summary>
    /// Gets the cumulative extra cooldown built up by successful casts (TOR's currentCooldownAddition).
    /// Persists across meetings, like TOR; reset at the start of each game (see WitchEvents).
    /// </summary>
    public float CurrentCooldownAddition { get; private set; }

    /// <summary>
    /// Resets the cumulative cooldown penalty. Called at game start - the button singleton outlives
    /// individual games, so the penalty would otherwise leak into the next lobby.
    /// </summary>
    public void ResetCooldownAddition()
    {
        CurrentCooldownAddition = 0f;
    }

    public override bool CanUse()
    {
        // No re-click during the channel; it either completes or self-cancels.
        return base.CanUse() && !EffectActive;
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        // TOR cancel rule: the channel breaks if the closest valid target stops being the player the
        // cast started on (they ran away, died, or someone closer walked in).
        if (EffectActive && castTarget != null && GetTarget() != castTarget)
        {
            CancelCast();
        }

        // GetTarget() is also called earlier this tick by the base CanUse() check; targetCached makes
        // both calls share one proximity scan instead of running it twice. Clear it here (the last
        // thing that runs each tick, per CustomActionButton.FixedUpdateHandler) so next tick recomputes.
        targetCached = false;
    }

    protected override void OnClick()
    {
        if (Target == null)
        {
            return;
        }

        castTarget = Target;

        // A cast duration of 0 means there is no effect phase at all - apply instantly.
        if (EffectDuration <= 0f)
        {
            CompleteCast();
        }
    }

    public override void OnEffectEnd()
    {
        CompleteCast();
    }

    private void CompleteCast()
    {
        if (castTarget == null || castTarget.HasDied())
        {
            castTarget = null;
            SetTimer(0f);
            return;
        }

        var options = OptionGroupSingleton<WitchOptions>.Instance;

        // Re-check for a race: with a borrowable kit and CastDuration reachable at 0 (instant cast),
        // two independent hex sources (e.g. native Witch + a Kirby holding a borrowed kit) can both
        // complete a cast on the same target near-simultaneously. Each RPC applies optimistically on
        // its own sender's client first, so without this recheck both casters' clients could
        // independently believe they own the hex - a cross-client desync that reaches the
        // hex-save/reveal logic at the meeting. Only the first cast to actually land should apply;
        // the loser just wastes its cooldown like a normal duplicate cast, nothing extra refunded.
        if (!castTarget.HasModifier<HexedModifier>())
        {
            castTarget.RpcAddModifier<HexedModifier>(PlayerControl.LocalPlayer);
        }

        castTarget = null;

        CurrentCooldownAddition += options.AdditionalCooldown;
        SetTimer(Cooldown);

        if (options.TriggerBothCooldowns)
        {
            PlayerControl.LocalPlayer.SetKillTimer(PlayerControl.LocalPlayer.GetKillCooldown());
        }
    }

    private void CancelCast()
    {
        castTarget = null;
        EffectActive = false;
        SetTimer(0f);
    }

    public override PlayerControl? GetTarget()
    {
        if (!targetCached)
        {
            var canHexAnyone = OptionGroupSingleton<WitchOptions>.Instance.CanHexAnyone;
            cachedTarget = PlayerControl.LocalPlayer.GetClosestLivingPlayer(canHexAnyone, Distance, false,
                x => !x.HasModifier<HexedModifier>());
            targetCached = true;
        }

        return cachedTarget;
    }
}
