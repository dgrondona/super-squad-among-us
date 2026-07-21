using MiraAPI.Events;
using MiraAPI.Events.Mira;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Hud;
using MiraAPI.Modifiers;
using SuperSquadAmongUs.Modifiers;

namespace SuperSquadAmongUs.Events;

/// <summary>
/// The granted vest's universal kill-block: while <see cref="GrantedVestModifier"/> is active, any kill
/// attempt or targeted ability aimed at the Gooper is cancelled. Copied from
/// <c>SuperSquadAmongUs.Events.ElusiveEvents</c>'s interception pattern but cancel-only (no teleport) -
/// a plain <c>BaseShieldModifier</c> on its own is not universally respected, only the handful of
/// TOU-Mira kill sources that explicitly check for it honor it (see docs/roles/gooper.md).
/// </summary>
public static class GooperEvents
{
    /// <summary>
    /// Local-only: a targeted ability button (kill or otherwise) clicked on a vested Gooper. Cancelling
    /// here prevents ClickHandler entirely - no cooldown or use is consumed.
    /// </summary>
    [RegisterEvent(1)]
    public static void MiraButtonClickEventHandler(MiraButtonClickEvent @event)
    {
        var button = @event.Button as CustomActionButton<PlayerControl>;
        var target = button?.Target;

        if (target == null || button == null || !button.CanClick())
        {
            return;
        }

        CheckForGooperVest(@event, PlayerControl.LocalPlayer, target);
    }

    /// <summary>
    /// All clients: a murder attempt against a vested Gooper.
    /// </summary>
    [RegisterEvent(1)]
    public static void BeforeMurderEventHandler(BeforeMurderEvent @event)
    {
        CheckForGooperVest(@event, @event.Source, @event.Target);
    }

    private static void CheckForGooperVest(MiraCancelableEvent miraEvent, PlayerControl source, PlayerControl target)
    {
        if (MeetingHud.Instance || ExileController.Instance)
        {
            return;
        }

        if (source == target || !target.HasModifier<GrantedVestModifier>())
        {
            return;
        }

        miraEvent.Cancel();
    }
}
