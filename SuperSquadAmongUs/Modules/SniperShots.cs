using System.Collections;
using MiraAPI.Modifiers;
using MiraAPI.Utilities.Assets;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using Reactor.Utilities;
using SuperSquadAmongUs.Assets;
using TownOfUs.Modifiers;
using TownOfUs.Utilities;
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
    /// Half-thickness of the bullet itself; each player's body radius is added on top, so the test is
    /// bullet-circle vs body-circle rather than line vs center point (a center-point test made shots
    /// whiff unless they passed within 0.2 units of the feet).
    /// </summary>
    public const float HitHalfWidth = 0.2f;

    // Used when a player's body sprite is unavailable (roughly half a player's visual height).
    private const float FallbackBodyRadius = 0.4f;

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
                player.Data.IsDead || player.Data.Disconnected || player.inVent)
            {
                continue;
            }

            if (!includeImpostors && player.IsImpostorAligned())
            {
                continue;
            }

            // Respect the same protections other TOU kill sources do: the first-death shield, and
            // states that make a player untargetable (devoured, ambush-hidden, ...).
            if (player.HasModifier<FirstDeadShield>() ||
                player.GetModifiers<DisabledModifier>().Any(x => !x.CanBeInteractedWith))
            {
                continue;
            }

            var (center, radius) = GetBodyCircle(player);
            var toPlayer = center - origin;
            var along = Vector2.Dot(toPlayer, direction);
            if (along < -radius)
            {
                continue;
            }

            var perpendicular = (toPlayer - (along * direction)).magnitude;
            if (perpendicular <= HitHalfWidth + radius)
            {
                hits.Add((player, along));
            }
        }

        return hits.OrderBy(x => x.Distance).Select(x => x.Player).ToList();
    }

    // A player's hittable area: their visible body sprite as a circle (what the shooter is aiming at),
    // falling back to the physics collider position if the sprite isn't available.
    private static (Vector2 Center, float Radius) GetBodyCircle(PlayerControl player)
    {
        var cosmetics = player.cosmetics;
        var body = cosmetics ? cosmetics!.currentBodySprite?.BodySprite : null;
        if (body)
        {
            var bounds = body!.bounds;
            return (bounds.center, Mathf.Max(bounds.extents.x, bounds.extents.y));
        }

        return (player.GetTruePosition(), FallbackBodyRadius);
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
