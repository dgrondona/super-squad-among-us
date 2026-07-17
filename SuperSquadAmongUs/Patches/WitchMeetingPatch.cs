using HarmonyLib;
using MiraAPI.Modifiers;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modifiers;
using UnityEngine;

namespace SuperSquadAmongUs.Patches;

/// <summary>
/// Shows the hex overlay sprite next to hexed players' names in meetings, visible to everyone (TOR's
/// SpellButtonMeeting overlay). Runs every MeetingHud update so hexes cast right before the meeting
/// and mid-meeting deaths both stay accurate.
/// </summary>
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
public static class WitchMeetingPatch
{
    private const string OverlayName = "SuperSquadHexedOverlay";

    [HarmonyPostfix]
    public static void Postfix(MeetingHud __instance)
    {
        foreach (var voteArea in __instance.playerStates)
        {
            var player = GameData.Instance.GetPlayerById(voteArea.TargetPlayerId)?.Object;
            var showOverlay = player != null && !player.Data.IsDead && player.HasModifier<HexedModifier>();

            var overlay = voteArea.transform.Find(OverlayName);
            if (showOverlay && overlay == null)
            {
                var overlayObject = new GameObject(OverlayName);
                overlayObject.transform.SetParent(voteArea.transform, false);
                overlayObject.transform.localPosition = new Vector3(-0.5f, -0.03f, -1f);

                // TOR takes the layer from the Megaphone child sprite, not the vote-area root - the
                // root's layer isn't guaranteed to be in the meeting camera's cull mask.
                overlayObject.layer = voteArea.Megaphone.gameObject.layer;

                var renderer = overlayObject.AddComponent<SpriteRenderer>();
                renderer.sprite = SuperSquadImpAssets.WitchHexedOverlaySprite.LoadAsset();
            }
            else if (!showOverlay && overlay != null)
            {
                UnityEngine.Object.Destroy(overlay.gameObject);
            }
        }
    }
}
