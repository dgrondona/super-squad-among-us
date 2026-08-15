using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Player;
using SuperSquadAmongUs.Modules;

namespace SuperSquadAmongUs.Events;

/// <summary>
/// Despawns the RC-XD's car the moment its deployer disconnects mid-drive. MiraAPI only runs a
/// button's <c>FixedUpdate</c> for <c>AmOwner</c>, so once the deployer is gone,
/// <c>RcXdDeployButton</c>'s own despawn path never runs again on any client - without this, the car
/// would sit inert on the map until <see cref="RcXdCarBehaviour"/>'s meeting-time safety net
/// eventually cleans it up.
/// </summary>
public static class RcXdEvents
{
    [RegisterEvent]
    public static void PlayerLeaveEventHandler(PlayerLeaveEvent @event)
    {
        var player = @event.ClientData.Character;
        if (player == null)
        {
            return;
        }

        var owner = RcXdCar.ActiveBehaviour?.Owner;
        if (owner != null && owner.PlayerId == player.PlayerId)
        {
            RcXdCar.EnsureDestroyedLocally();
        }
    }
}
