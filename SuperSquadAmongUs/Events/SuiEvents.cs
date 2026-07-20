using MiraAPI.Events;
using MiraAPI.Events.Mira;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Modifiers;
using SuperSquadAmongUs.Buttons.Crewmate;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Options.Roles.Crewmate;
using TownOfUs.Utilities;

namespace SuperSquadAmongUs.Events;

/// <summary>
/// The Sui's protection detection: while a player is protected, an interaction with them arms Sui's
/// one-shot retaliation kill on whoever did it (see <see cref="SuiRetaliateButton"/>). Unlike
/// Elusive's shield, the interaction is never cancelled - it proceeds normally; Sui only gains a
/// reaction, not a block. Template copied from <see cref="ElusiveEvents"/>: local-only for
/// <see cref="MiraButtonClickEvent"/> (fires on the attacker's own client, before ClickHandler),
/// deterministic for <see cref="BeforeMurderEvent"/> since protection state arrived via a synced
/// modifier (house rule, see docs/il2cpp-gotchas.md).
/// </summary>
public static class SuiEvents
{
    /// <summary>
    /// Local-only: a targeted ability button (kill or otherwise) clicked on a protected player. Skipped
    /// entirely when "only impostor kills trigger" is on - that mode is narrowed to actual kill
    /// attempts, handled by <see cref="BeforeMurderEventHandler"/> instead.
    /// </summary>
    [RegisterEvent(1)]
    public static void MiraButtonClickEventHandler(MiraButtonClickEvent @event)
    {
        if (OptionGroupSingleton<SuiOptions>.Instance.OnlyImpostorKillsTrigger)
        {
            return;
        }

        var button = @event.Button as CustomActionButton<PlayerControl>;
        var source = PlayerControl.LocalPlayer;
        var target = button?.Target;

        if (target == null || button == null || !button.CanClick())
        {
            return;
        }

        CheckForSuiTrigger(source, target);
    }

    /// <summary>
    /// All clients: a murder attempt against a protected player.
    /// </summary>
    [RegisterEvent(1)]
    public static void BeforeMurderEventHandler(BeforeMurderEvent @event)
    {
        CheckForSuiTrigger(@event.Source, @event.Target);
    }

    private static void CheckForSuiTrigger(PlayerControl source, PlayerControl target)
    {
        if (MeetingHud.Instance || ExileController.Instance)
        {
            return;
        }

        if (source == target || !target.TryGetModifier<SuiProtectedModifier>(out var mod))
        {
            return;
        }

        if (OptionGroupSingleton<SuiOptions>.Instance.OnlyImpostorKillsTrigger && !source.IsImpostorAligned())
        {
            return;
        }

        // Only the Sui's own client arms their own retaliation button.
        if (mod.Sui.AmOwner)
        {
            CustomButtonSingleton<SuiRetaliateButton>.Instance.Arm(source);
        }
    }
}
