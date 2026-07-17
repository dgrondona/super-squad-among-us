namespace SuperSquadAmongUs.Modules;

/// <summary>
/// RPC ids for this addon's Reactor <c>[MethodRpc]</c>s. Reactor scopes ids per plugin, so these only
/// need to be unique within SuperSquadAmongUs - collisions with TOU-Mira/MiraAPI ids are fine.
/// </summary>
public enum SuperSquadRpc : uint
{
    PlaceNinjaTrace = 1,
    ShowSniperShot = 2,
    MafiaCleanBody = 3,
    VultureEatBody = 4,
}
