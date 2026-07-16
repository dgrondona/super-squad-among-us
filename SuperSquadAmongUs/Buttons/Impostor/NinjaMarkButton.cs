using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Modifiers;
using MiraAPI.Networking;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Modules;
using SuperSquadAmongUs.Options.Roles.Impostor;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs;
using TownOfUs.Buttons;
using TownOfUs.Modifiers;
using TownOfUs.Modifiers.Neutral;
using TownOfUs.Modules.Localization;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Impostor;

/// <summary>
/// The Ninja's two-state button (TOR convention): first press marks the closest target and starts a
/// short 5s arming delay; second press assassinates the marked target from anywhere on the map
/// (teleport kill), leaves traces at both ends, and turns the Ninja invisible.
/// </summary>
public sealed class NinjaMarkButton : TownOfUsKillRoleButton<NinjaRole, PlayerControl>, IKillButton
{
    // Fixed arming delay between marking and being able to assassinate, from TOR (Buttons.cs sets
    // ninjaButton.Timer = 5f on mark).
    private const float MarkArmingDelay = 5f;

    public override string Name => TouLocale.GetParsed("SuperSquadRoleNinjaMark", "Mark");
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => TownOfUsColors.Impostor;
    public override float Cooldown => Math.Clamp(OptionGroupSingleton<NinjaOptions>.Instance.MarkCooldown + MapCooldown, 5f, 120f);
    public override LoadableAsset<Sprite> Sprite => SuperSquadImpAssets.NinjaMarkSprite;

    /// <summary>
    /// Gets the currently marked target, or null when no mark is armed. Local to the Ninja's client -
    /// nothing about the mark needs syncing until the assassinate RPCs fire.
    /// </summary>
    public PlayerControl? Marked { get; private set; }

    public override bool CanUse()
    {
        if (Marked == null)
        {
            return base.CanUse();
        }

        // Assassinate phase: works from anywhere, no proximity target needed. TOR gates the strike on
        // CanMove (no assassinating from inside a vent) and on the target not being vented either.
        if (HudManager.Instance.Chat.IsOpenOrOpening || MeetingHud.Instance || PlayerControl.LocalPlayer.HasDied() ||
            !PlayerControl.LocalPlayer.CanMove || Marked.inVent)
        {
            return false;
        }

        return Timer <= 0;
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        // The mark doesn't survive meetings, the target dying first, or the ninja dying (TOR hides the
        // arrow for a dead ninja). The marked modifier cleans up its own arrow; this resets the button.
        if (Marked != null && (MeetingHud.Instance || Marked.HasDied() || playerControl.HasDied()))
        {
            ClearMark();
        }
    }

    protected override void OnClick()
    {
        if (Marked == null)
        {
            Mark();
        }
        else
        {
            Assassinate();
        }
    }

    public override void ClickHandler()
    {
        // Mirror TownOfUsButton.ClickHandler's gating - overriding it must not bypass the
        // hacked/disabled checks every other TOU button gets.
        if (!CanClick() || PlayerControl.LocalPlayer.HasModifier<GlitchHackedModifier>() ||
            PlayerControl.LocalPlayer.HasModifier<DisabledModifier>())
        {
            return;
        }

        var wasMarked = Marked != null;
        OnClick();
        Button?.SetDisabled();
        SetTimer(wasMarked ? Cooldown : MarkArmingDelay);
    }

    private void Mark()
    {
        if (Target == null)
        {
            return;
        }

        Marked = Target;

        // Arrow is Ninja-local (never synced): add the modifier directly instead of via RPC.
        if (OptionGroupSingleton<NinjaOptions>.Instance.KnowsTargetLocation)
        {
            var modifier = new NinjaMarkedModifier(PlayerControl.LocalPlayer, TownOfUsColors.Impostor, 0f);
            Marked.GetModifierComponent()!.AddModifier(modifier);
        }

        OverrideName(TouLocale.GetParsed("SuperSquadRoleNinjaAssassinate", "Assassinate"));
        OverrideSprite(SuperSquadImpAssets.NinjaAssassinateSprite.LoadAsset());
    }

    private void Assassinate()
    {
        var target = Marked!;
        ClearMark();

        // TOR's exact sequence: launch trace -> invisibility -> teleport murder -> landing trace.
        var ninja = PlayerControl.LocalPlayer;
        var launchPosition = ninja.transform.position;
        NinjaTraces.RpcPlaceNinjaTrace(ninja, launchPosition.x, launchPosition.y);

        if (OptionGroupSingleton<NinjaOptions>.Instance.InvisibleDuration > 0f)
        {
            ninja.RpcAddModifier<NinjaInvisibleModifier>();
        }

        var landingPosition = target.transform.position;
        ninja.RpcCustomMurder(target);
        NinjaTraces.RpcPlaceNinjaTrace(ninja, landingPosition.x, landingPosition.y);
    }

    private void ClearMark()
    {
        if (Marked != null && Marked.HasModifier<NinjaMarkedModifier>())
        {
            Marked.RemoveModifier<NinjaMarkedModifier>();
        }

        Marked = null;
        OverrideName(TouLocale.GetParsed("SuperSquadRoleNinjaMark", "Mark"));
        OverrideSprite(SuperSquadImpAssets.NinjaMarkSprite.LoadAsset());
    }

    public override PlayerControl? GetTarget()
    {
        return PlayerControl.LocalPlayer.GetClosestLivingPlayer(false, Distance);
    }
}
