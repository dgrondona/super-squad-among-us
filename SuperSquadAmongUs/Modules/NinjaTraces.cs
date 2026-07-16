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
/// TOR's NinjaTrace: tinted with the Ninja's player color, lerping to green over the color-fade
/// option, then alpha-fading out at the end of the trace duration.
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

        var startColor = Palette.PlayerColors[source.Data.DefaultOutfit.ColorId];
        Coroutines.Start(CoAnimateTrace(renderer, startColor, options.TraceColorFadeDuration, options.TraceDuration));
    }

    private static IEnumerator CoAnimateTrace(SpriteRenderer renderer, Color startColor, float colorFadeDuration, float traceDuration)
    {
        // TOR keeps the trace fully opaque until the last stretch, then alpha-fades it out over at
        // most one second (or half the trace time for very short traces).
        var fadeOutDuration = Mathf.Min(1f, traceDuration / 2f);
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
