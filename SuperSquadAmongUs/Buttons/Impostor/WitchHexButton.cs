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
public sealed class WitchHexButton : TownOfUsRoleButton<WitchRole, PlayerControl>
{
    private PlayerControl? castTarget;

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

    /// <inheritdoc />
    public override void CreateButton(Transform parent)
    {
        base.CreateButton(parent);

        // The ability icon already shows the name (user request 2026-07-16) - the redundant text
        // label underneath is hidden. Sprite/cooldown/click handling are untouched.
        if (Button?.buttonLabelText != null)
        {
            Button.buttonLabelText.gameObject.SetActive(false);
        }
    }

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

        castTarget.RpcAddModifier<HexedModifier>(PlayerControl.LocalPlayer);
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
        var canHexAnyone = OptionGroupSingleton<WitchOptions>.Instance.CanHexAnyone;
        return PlayerControl.LocalPlayer.GetClosestLivingPlayer(canHexAnyone, Distance, false,
            x => !x.HasModifier<HexedModifier>());
    }
}
