using System.Collections;
using MiraAPI.Utilities.Assets;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using Reactor.Utilities;
using SuperSquadAmongUs.Assets;
using UnityEngine;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// The Sniper's bullet: hit math and the travel visual. The shot is an infinite piercing line from the
/// sniper's body through the clicked point - it goes through walls and kills everyone within
/// <see cref="HitHalfWidth"/> of the line (user design; ATR's closest-target-only cone is not used).
/// The visual is only shown to others when the "bullet visible" option is on, via the RPC below.
/// </summary>
public static class SniperShots
{
    /// <summary>
    /// Perpendicular distance from the shot line within which a player is hit (ATR's 0.2-unit width).
    /// </summary>
    public const float HitHalfWidth = 0.2f;

    // How far the travel visual flies and how fast, purely cosmetic (the kill line is infinite).
    private const float VisualRange = 60f;
    private const float VisualSpeed = 40f;

    /// <summary>
    /// Finds every living player pierced by a shot fired from origin toward direction.
    /// </summary>
    /// <param name="shooter">The sniper (never hit by their own bullet).</param>
    /// <param name="origin">The shot's start point (the sniper's body).</param>
    /// <param name="direction">Normalized aim direction.</param>
    /// <param name="includeImpostors">Whether fellow impostors can be hit.</param>
    /// <returns>All players on the line, ordered by distance.</returns>
    public static List<PlayerControl> FindHits(PlayerControl shooter, Vector2 origin, Vector2 direction, bool includeImpostors)
    {
        var hits = new List<(PlayerControl Player, float Distance)>();

        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player == null || player.PlayerId == shooter.PlayerId || player.Data == null ||
                player.Data.IsDead || player.Data.Disconnected)
            {
                continue;
            }

            if (!includeImpostors && player.Data.Role != null && player.Data.Role.IsImpostor)
            {
                continue;
            }

            var toPlayer = (Vector2)player.GetTruePosition() - origin;
            var along = Vector2.Dot(toPlayer, direction);
            if (along < 0f)
            {
                continue;
            }

            var perpendicular = (toPlayer - (along * direction)).magnitude;
            if (perpendicular <= HitHalfWidth)
            {
                hits.Add((player, along));
            }
        }

        return hits.OrderBy(x => x.Distance).Select(x => x.Player).ToList();
    }

    /// <summary>
    /// Shows the bullet travel visual on every client (used when the bullet-visible option is on).
    /// </summary>
    /// <param name="source">The sniper.</param>
    /// <param name="originX">Shot origin x.</param>
    /// <param name="originY">Shot origin y.</param>
    /// <param name="directionX">Normalized direction x.</param>
    /// <param name="directionY">Normalized direction y.</param>
    [MethodRpc((uint)SuperSquadRpc.ShowSniperShot, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcShowShot(PlayerControl source, float originX, float originY, float directionX, float directionY)
    {
        ShowShotLocally(new Vector2(originX, originY), new Vector2(directionX, directionY));
    }

    /// <summary>
    /// Shows the bullet travel visual on this client only (used for the sniper themself when the
    /// bullet-visible option is off).
    /// </summary>
    /// <param name="origin">Shot origin.</param>
    /// <param name="direction">Normalized direction.</param>
    public static void ShowShotLocally(Vector2 origin, Vector2 direction)
    {
        Coroutines.Start(CoBulletTravel(origin, direction));
    }

    private static IEnumerator CoBulletTravel(Vector2 origin, Vector2 direction)
    {
        var bullet = new GameObject("SuperSquadSniperBullet");
        var renderer = bullet.AddComponent<SpriteRenderer>();
        renderer.sprite = SuperSquadImpAssets.SniperGuideSprite.LoadAsset();
        bullet.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        var travelled = 0f;
        while (travelled < VisualRange)
        {
            travelled += VisualSpeed * Time.deltaTime;
            var position = origin + (direction * travelled);
            bullet.transform.position = new Vector3(position.x, position.y, position.y / 1000f - 1f);
            yield return null;
        }

        UnityEngine.Object.Destroy(bullet);
    }
}
