using MiraAPI.Keybinds;
using UnityEngine;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// Fixes the keybind-sharing collision created by <see cref="AbilityGrants"/>: MiraAPI
/// fires every enabled button bound to a keybind independently, with no coordination
/// between them (see reference/MiraAPI/MiraAPI/Hud/CustomActionButton.cs). Normally
/// harmless - a role has one ability per keybind - but a grant-holder (Gooper/Kirby) can
/// end up with two unrelated abilities sharing a slot at once. See docs/roles/gooper.md's
/// "Keybind allocation" note.
/// </summary>
public static class KeybindArbiter
{
    private static readonly Dictionary<BaseKeybind, int> ClaimedFrames = new();

    /// <summary>
    /// Attempts to claim <paramref name="keybind"/> for the current frame. Null (a
    /// click-only button) always succeeds.
    /// </summary>
    /// <param name="keybind">The keybind the calling button is bound to.</param>
    /// <returns>
    /// True if this button may proceed to fire (either the first claimant this frame, or
    /// a different keybind entirely); false if another button already claimed this same
    /// keybind this frame.
    /// </returns>
    public static bool TryClaim(BaseKeybind? keybind)
    {
        if (keybind == null)
        {
            return true;
        }

        var frame = Time.frameCount;
        if (ClaimedFrames.TryGetValue(keybind, out var claimedFrame) && claimedFrame == frame)
        {
            return false;
        }

        ClaimedFrames[keybind] = frame;
        return true;
    }
}
