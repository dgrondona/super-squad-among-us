using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Options.Roles.Impostor;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs.Events;
using TownOfUs.Modifiers;
using TownOfUs.Modules.Localization;
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

            // The hex fizzles if the witch no longer holds the role (role swap/disconnect), or if the
            // witch was exiled this meeting with the save option on. Note the TOR asymmetry: a witch
            // merely *killed* during the meeting does NOT save the targets - only being voted out does.
            var witchInvalid = witch == null || witch.Data == null ||
                               witch.Data.Disconnected || witch.Data.Role is not WitchRole;
            var savedByVote = voteSaves && exiled != null && witch == exiled;

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
}
