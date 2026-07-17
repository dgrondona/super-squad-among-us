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
/// The Eraser's mark (TOR): target a player to strip their modded role at the next meeting's exile
/// screen. Every use permanently adds 10s to the cooldown (TOR's <c>MaxTimer += 10</c>) - the
/// penalty persists across meetings and only resets at game start (see EraserEvents).
/// </summary>
public sealed class EraserEraseButton : TownOfUsRoleButton<EraserRole, PlayerControl>
{
    // TOR's fixed cooldown escalation per successful erase (Buttons.cs:1103).
    private const float CooldownEscalation = 10f;

    public override string Name => TouLocale.GetParsed("SuperSquadRoleEraserErase", "Erase");
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => TownOfUsColors.Impostor;
    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<EraserOptions>.Instance.EraseCooldown + CurrentCooldownAddition + MapCooldown, 5f, 240f);
    public override LoadableAsset<Sprite> Sprite => SuperSquadImpAssets.EraserSprite;

    /// <summary>
    /// Gets the cumulative extra cooldown built up by uses this game (TOR escalation). Persists
    /// across meetings; reset at game start via <see cref="ResetCooldownAddition"/>.
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
    /// Resets the cumulative cooldown escalation. Called at game start - the button singleton
    /// outlives individual games.
    /// </summary>
    public void ResetCooldownAddition()
    {
        CurrentCooldownAddition = 0f;
    }

    protected override void OnClick()
    {
        if (Target == null)
        {
            return;
        }

        Target.RpcAddModifier<FutureErasedModifier>(PlayerControl.LocalPlayer);

        CurrentCooldownAddition += CooldownEscalation;
        SetTimer(Cooldown);
    }

    public override PlayerControl? GetTarget()
    {
        var canEraseAnyone = OptionGroupSingleton<EraserOptions>.Instance.CanEraseAnyone;
        return PlayerControl.LocalPlayer.GetClosestLivingPlayer(canEraseAnyone, Distance, false,
            x => !x.HasModifier<FutureErasedModifier>());
    }
}
