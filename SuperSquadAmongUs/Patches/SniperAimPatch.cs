using HarmonyLib;
using MiraAPI.Hud;
using SuperSquadAmongUs.Buttons.Impostor;
using SuperSquadAmongUs.Buttons.Neutral;

namespace SuperSquadAmongUs.Patches;

/// <summary>
/// Drives the Sniper's click-to-fire from <see cref="HudManager.Update"/> instead of the button's own
/// FixedUpdate, which doesn't run on every rendered frame and drops quick clicks (same pitfall as
/// <see cref="ApparaterMapClickPatch"/>). <see cref="SniperSnipeButton.HandleAimFrame"/> self-guards
/// and is a no-op unless the local player has an active aim window. Also drives every role that has
/// granted itself the Snipe pool ability (Gooper, Kirby - see
/// <see cref="SuperSquadAmongUs.Buttons.GrantedSnipeButtonBase{TRole}"/>); each new granted-Snipe
/// button singleton needs a line here, easy to forget when the pool grows (see docs/roles/gooper.md).
/// Runs at <see cref="Priority.First"/>: when any earlier postfix on the same method throws, Harmony
/// skips the remaining postfixes for that frame, and TOU-Mira hangs several patches off
/// HudManager.Update - being skipped on the one frame a click happened silently eats the shot. For
/// the same reason in reverse, our handler is exception-guarded so we can never break the others.
/// </summary>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class SniperAimPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    public static void Postfix()
    {
        try
        {
            CustomButtonSingleton<SniperSnipeButton>.Instance.HandleAimFrame();
        }
        catch (Exception e)
        {
            Error($"Sniper: HandleAimFrame threw: {e}");
        }

        try
        {
            CustomButtonSingleton<GooperSnipeButton>.Instance.HandleAimFrame();
        }
        catch (Exception e)
        {
            Error($"Gooper: HandleAimFrame threw: {e}");
        }

        try
        {
            CustomButtonSingleton<KirbySnipeButton>.Instance.HandleAimFrame();
        }
        catch (Exception e)
        {
            Error($"Kirby: HandleAimFrame threw: {e}");
        }
    }
}
