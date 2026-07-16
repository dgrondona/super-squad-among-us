using HarmonyLib;
using SuperSquadAmongUs.Roles.Impostor;

namespace SuperSquadAmongUs.Patches;

/// <summary>
/// Shows the mafia their team, TOR-style (UpdatePatch.cs:176): members see "(G)", "(M)", "(J)"
/// appended to each other's names in the world and in meetings. Non-mafia players never see the
/// tags. Appended idempotently every frame so name rewrites by TOU-Mira just lose the tag for a
/// single frame. Like TOR, there's no intro reveal - the tags are how the team finds each other.
/// </summary>
public static class MafiaLabelsPatch
{
    private static bool LocalIsMafia =>
        PlayerControl.LocalPlayer != null &&
        PlayerControl.LocalPlayer.Data?.Role is GodfatherRole or MafiosoRole or MafiaJanitorRole;

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    [HarmonyPostfix]
    public static void HudUpdatePostfix()
    {
        if (!ShipStatus.Instance || !LocalIsMafia)
        {
            return;
        }

        foreach (var player in PlayerControl.AllPlayerControls)
        {
            var tag = GetTag(player);
            if (tag == null || player.cosmetics == null || player.cosmetics.nameText == null)
            {
                continue;
            }

            if (!player.cosmetics.nameText.text.EndsWith(tag))
            {
                player.cosmetics.nameText.text += $" {tag}";
            }
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
    [HarmonyPostfix]
    public static void MeetingUpdatePostfix(MeetingHud __instance)
    {
        if (!LocalIsMafia)
        {
            return;
        }

        foreach (var voteArea in __instance.playerStates)
        {
            var player = GameData.Instance.GetPlayerById(voteArea.TargetPlayerId)?.Object;
            var tag = GetTag(player);
            if (tag == null || voteArea.NameText == null)
            {
                continue;
            }

            if (!voteArea.NameText.text.EndsWith(tag))
            {
                voteArea.NameText.text += $" {tag}";
            }
        }
    }

    // Tags key off the live role, so they disappear when a member dies (their role becomes a ghost
    // role) - a small deviation from TOR, where dead mafia stay labeled.
    private static string? GetTag(PlayerControl? player)
    {
        return player?.Data?.Role switch
        {
            GodfatherRole => "(G)",
            MafiosoRole => "(M)",
            MafiaJanitorRole => "(J)",
            _ => null,
        };
    }
}
