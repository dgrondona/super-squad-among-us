using UnityEngine;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// Classifies a clicked point on the open minimap by sampling the map background sprite's own texture
/// (<c>MapBehaviour.ColorControl.rend</c>): the vanilla map texture is fully transparent over walls and
/// off-map space and semi-transparent over rooms and hallways, so texture alpha at the click IS the
/// "is this somewhere on the ship" answer - no world-geometry probing involved. Game textures aren't
/// CPU-readable, so a readable copy is blitted once per texture and cached (rebuilt when the map -
/// and therefore the texture - changes between games).
/// </summary>
internal static class MiniMapMask
{
    private static Texture2D? readableCopy;
    private static int readableSourceId;

    /// <summary>
    /// Samples the minimap background's texture alpha at a world-space point (a raw
    /// <c>Camera.main.ScreenToWorldPoint</c> click while the map is open).
    /// </summary>
    /// <param name="worldPoint">The click in world space, NOT map-scaled ship space.</param>
    /// <param name="alpha">The sampled texture alpha; 0 for points outside the map sprite.</param>
    /// <returns>False when sampling isn't possible (no map open, no sprite, copy failed) - the caller
    /// should fall back to its geometry-based validation rather than rejecting the click.</returns>
    public static bool TryGetAlphaAtWorldPoint(Vector2 worldPoint, out float alpha)
    {
        alpha = 0f;

        var map = MapBehaviour.Instance;
        if (!map)
        {
            return false;
        }

        var renderer = map.ColorControl != null ? map.ColorControl.rend : null;
        var sprite = renderer != null ? renderer.sprite : null;
        if (renderer == null || sprite == null)
        {
            return false;
        }

        var texture = GetReadableCopy(sprite.texture);
        if (texture == null)
        {
            return false;
        }

        // Sprite-local units -> texture pixels. Sprite.pivot is in pixels within the sprite rect
        // (the runtime API; only the importer setting is normalized), and rect.x/y offset into the
        // atlas page if the sprite is packed.
        //
        // This samples against the RENDERER's transform (ColorControl.rend, on the map's Background
        // object), which is deliberately NOT the frame ApparaterMapButton.GetRawClickWorldPosition
        // converts in (HerePoint's parent). The two are separated by a per-map translation; each is
        // correct for its own job, and this one must stay in displayed-sprite space - that's also why
        // mirrored maps (Dleks) need no sign flip here while the ship-space conversion does.
        var local = (Vector2)renderer.transform.InverseTransformPoint(worldPoint);
        var px = sprite.rect.x + sprite.pivot.x + (local.x * sprite.pixelsPerUnit);
        var py = sprite.rect.y + sprite.pivot.y + (local.y * sprite.pixelsPerUnit);

        if (px < sprite.rect.xMin || px >= sprite.rect.xMax || py < sprite.rect.yMin || py >= sprite.rect.yMax)
        {
            // Outside the sprite entirely - classified successfully as off-map.
            return true;
        }

        alpha = texture.GetPixel((int)px, (int)py).a;
        return true;
    }

    // The renderer tint (BareMapVisuals' ColorControl.SetColor) only affects the material color, never
    // the texture pixels sampled here, so the copy stays valid across recolors.
    private static Texture2D? GetReadableCopy(Texture2D? source)
    {
        if (source == null)
        {
            return null;
        }

        if (readableCopy != null && readableSourceId == source.GetInstanceID())
        {
            return readableCopy;
        }

        if (readableCopy != null)
        {
            UnityEngine.Object.Destroy(readableCopy);
            readableCopy = null;
        }

        var temp = RenderTexture.GetTemporary(source.width, source.height, 0);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(source, temp);
            RenderTexture.active = temp;

            var copy = new Texture2D(source.width, source.height, TextureFormat.ARGB32, false);
            copy.ReadPixels(new Rect(0, 0, temp.width, temp.height), 0, 0);
            copy.Apply();

            readableCopy = copy;
            readableSourceId = source.GetInstanceID();
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temp);
        }

        return readableCopy;
    }
}
