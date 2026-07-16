using System.Collections;
using MiraAPI.GameOptions;
using MiraAPI.Utilities.Assets;
using Reactor.Networking.Attributes;
using Reactor.Networking.Rpc;
using Reactor.Utilities;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Options.Roles.Impostor;
using UnityEngine;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// World-space "someone dashed through here" markers left by the Ninja's assassination, one at the
/// Ninja's launch position and one at the victim. Unlike the tracking arrow these are real scene
/// objects every client renders (and cameras can see), so placement is RPC-synced. Visual follows
/// TOR's NinjaTrace exactly, including its quirk: the "tint" isn't actually the Ninja's player color -
/// TOR's isLighterColor(PlayerControl) is just `playerId % 2 == 0` (Helpers.cs), so the start color is
/// white for even-ID Ninjas and black (Palette.PlayerColors[6]) for odd-ID ones, before lerping to
/// green over the color-fade option and alpha-fading out at the end of the trace duration.
/// </summary>
public static class NinjaTraces
{
    /// <summary>
    /// Places a trace at the given position on every client.
    /// </summary>
    /// <param name="source">The ninja whose player color tints the trace.</param>
    /// <param name="x">World x position.</param>
    /// <param name="y">World y position.</param>
    [MethodRpc((uint)SuperSquadRpc.PlaceNinjaTrace, LocalHandling = RpcLocalHandling.Before)]
    public static void RpcPlaceNinjaTrace(PlayerControl source, float x, float y)
    {
        var options = OptionGroupSingleton<NinjaOptions>.Instance;
        var traceObject = new GameObject("SuperSquadNinjaTrace");
        traceObject.transform.position = new Vector3(x, y, y / 1000f + 0.01f);

        var renderer = traceObject.AddComponent<SpriteRenderer>();
        renderer.sprite = SuperSquadImpAssets.NinjaTraceSprite.LoadAsset();

        // TOR's isLighterColor is a player-ID parity check, not an actual color comparison - replicated
        // faithfully rather than "fixed" to use the Ninja's real outfit color.
        var startColor = source.PlayerId % 2 == 0 ? Color.white : (Color)Palette.PlayerColors[6];
        Coroutines.Start(CoAnimateTrace(renderer, startColor, options.TraceColorFadeDuration, options.TraceDuration));
    }

    private static IEnumerator CoAnimateTrace(SpriteRenderer renderer, Color startColor, float colorFadeDuration, float traceDuration)
    {
        // TOR keeps the trace fully opaque until the last stretch, then alpha-fades it out over at
        // most one second (or half the trace time for very short traces).
        // TOR's exact rule (NinjaTrace.cs): 1s fade, shrunk to half the lifetime for sub-1s traces.
        var fadeOutDuration = traceDuration >= 1f ? 1f : 0.5f * traceDuration;
        var elapsed = 0f;

        while (elapsed < traceDuration)
        {
            if (!renderer)
            {
                yield break;
            }

            var color = colorFadeDuration <= 0f
                ? Color.green
                : Color.Lerp(startColor, Color.green, Mathf.Clamp01(elapsed / colorFadeDuration));

            var fadeStart = traceDuration - fadeOutDuration;
            color.a = elapsed <= fadeStart ? 1f : Mathf.Clamp01(1f - ((elapsed - fadeStart) / fadeOutDuration));

            renderer.color = color;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (renderer)
        {
            UnityEngine.Object.Destroy(renderer.gameObject);
        }
    }
}
