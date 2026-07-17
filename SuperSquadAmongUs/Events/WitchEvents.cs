using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Modifiers;
using SuperSquadAmongUs.Buttons.Impostor;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Options.Roles.Impostor;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs.Events;
using TownOfUs.Modifiers;
using TownOfUs.Modifiers.Game.Alliance;
using TownOfUs.Modules.Localization;
using TownOfUs.Options.Modifiers.Alliance;
using TownOfUs.Utilities;

namespace SuperSquadAmongUs.Events;

/// <summary>
/// Resolves the Witch's hexes when a meeting ends (at the ejection screen, matching TOR's
/// ExileController hook): every hexed player dies exile-style (no body) UNLESS the Witch was the one
/// voted out and the save option is on. Runs deterministically on every client - hex state arrived
/// via synced modifiers, so no RPCs are needed here.
/// </summary>
public static class WitchEvents
{
    /// <summary>
    /// Resets the hex cooldown penalty at game start. TOR keeps the penalty across meetings and only
    /// clears it on game reset; the button singleton outlives games, so this is where that happens.
    /// </summary>
    [RegisterEvent]
    public static void RoundStartEventHandler(RoundStartEvent @event)
    {
        if (@event.TriggeredByIntro)
        {
            CustomButtonSingleton<WitchHexButton>.Instance.ResetCooldownAddition();
        }
    }

    [RegisterEvent]
    public static void EjectionEventHandler(EjectionEvent @event)
    {
        var exiled = @event.ExileController?.initData?.networkedPlayer?.Object;
        var voteSaves = OptionGroupSingleton<WitchOptions>.Instance.VotingWitchSavesTargets;

        foreach (var hex in ModifierUtils.GetActiveModifiers<HexedModifier>().ToList())
        {
            var witch = hex.Witch;
            var target = hex.Player;
            hex.ModifierComponent?.RemoveModifier(hex);

            // The hex fizzles if the witch disconnected or was role-swapped while alive, or if the
            // witch was exiled this meeting with the save option on. Note the TOR asymmetry: a witch
            // merely *killed* does NOT save the targets - her hexes still fire (a dead player's
            // Data.Role is their ghost role, so the WitchRole check must only apply to the living).
            // TOR also saves the targets when the witch is a lover dying alongside her exiled partner.
            var witchInvalid = witch == null || witch.Data == null || witch.Data.Disconnected ||
                               (!witch.HasDied() && witch.Data.Role is not WitchRole);
            var savedByVote = !witchInvalid && voteSaves && exiled != null &&
                              (witch == exiled || WitchDiesWithExiledLover(witch!, exiled));

            if (witchInvalid || savedByVote || target == null || target.HasDied() || target == exiled)
            {
                continue;
            }

            DeathHandlerModifier.UpdateDeathHandlerImmediate(
                target,
                TouLocale.Get("SuperSquadDiedToWitchHex", "Hexed"),
                DeathEventHandlers.CurrentRound,
                DeathHandlerOverride.SetFalse,
                TouLocale.GetParsed("DiedByStringBasic").Replace("<player>", witch!.Data.PlayerName),
                lockInfo: DeathHandlerOverride.SetTrue);

            target.Exiled();
        }
    }

    // TOR's witchDiesWithExiledLover: the witch is a lover, lovers die together, and the exiled
    // player is her partner - the vote effectively killed the witch, so the save rule applies.
    private static bool WitchDiesWithExiledLover(PlayerControl witch, PlayerControl exiled)
    {
        return OptionGroupSingleton<LoversOptions>.Instance.BothLoversDie &&
               witch.TryGetModifier<LoverModifier>(out var loveMod) &&
               loveMod!.OtherLover != null && loveMod.OtherLover == exiled;
    }
}
