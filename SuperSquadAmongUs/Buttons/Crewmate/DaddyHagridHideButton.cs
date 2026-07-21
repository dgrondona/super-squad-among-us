using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Modifiers;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Options.Roles.Crewmate;
using SuperSquadAmongUs.Roles.Crewmate;
using TownOfUs.Buttons;
using TownOfUs.Modifiers;
using TownOfUs.Modifiers.Neutral;
using TownOfUs.Modules.Localization;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Crewmate;

/// <summary>
/// Daddy Hagrid's Hide/Release toggle: tuck the nearest player into the cloak (see
/// <see cref="CloakHiddenModifier"/>), or release them early while hiding. The hide runs as a cancellable
/// button EFFECT so the player sees a fill-up countdown of the remaining hide time and can release early
/// mid-countdown (the RC-XD/Dumper pattern). It auto-releases when the effect times out; a meeting or the
/// Hagrid's death pop the player out via the modifier/events, which this button then detects to end the
/// effect. One player at a time. Deliberately NOT an <see cref="IKillButton"/> - hiding someone is not an
/// attack (though TOU-global rules still apply: clicking an alerted Veteran retaliates).
/// </summary>
public sealed class DaddyHagridHideButton : SuperSquadRoleButton<DaddyHagridRole, PlayerControl>
{
    private static string HideLabel => TouLocale.GetParsed("SuperSquadRoleDaddyHagridHide", "Hide");
    private static string ReleaseLabel => TouLocale.GetParsed("SuperSquadRoleDaddyHagridRelease", "Release");

    // The player currently hidden by THIS Hagrid, or null. The button's EffectActive is the phase
    // authority (Hide vs. Release); this field just remembers WHO to release. Not re-derived from
    // HasModifier each frame - a freshly-sent RpcAddModifier isn't visible on the target for a tick or
    // two, so the sync-settle grace below guards the "cloak gone" detection.
    private PlayerControl? hiddenPlayer;

    // When the current player was hidden (local Time.time), for the sync-settle grace.
    private float hideTime = float.NegativeInfinity;
    private const float SyncSettleTime = 2f;

    // Guards one physical press dispatching twice (keybind + click, or Proton key autorepeat).
    private const float ToggleDebounce = 0.3f;
    private float lastToggleTime = float.NegativeInfinity;

    /// <inheritdoc />
    public override string Name => HideLabel;

    /// <inheritdoc />
    public override BaseKeybind Keybind => Keybinds.PrimaryAction;

    /// <inheritdoc />
    public override Color TextOutlineColor => SuperSquadColors.DaddyHagrid;

    /// <inheritdoc />
    public override float Cooldown => Math.Clamp(
        OptionGroupSingleton<DaddyHagridOptions>.Instance.HideCooldown + MapCooldown, 5f, 120f);

    // The hide duration IS the effect duration - that's what drives the visible fill-up countdown.
    // Cancellable so a mid-hide press releases early instead of being ignored.
    public override float EffectDuration => OptionGroupSingleton<DaddyHagridOptions>.Instance.HideDuration;
    public override bool IsEffectCancellable() => true;

    /// <inheritdoc />
    public override int MaxUses => (int)OptionGroupSingleton<DaddyHagridOptions>.Instance.MaxUses;

    /// <inheritdoc />
    public override LoadableAsset<Sprite> Sprite => SuperSquadAssets.CrewmatePlaceholderButton;

    // Keep ticking while the effect is live even if the role stops matching (e.g. Hagrid dies mid-hide),
    // so FixedUpdate can still end the effect and clear stale state - same reason RC-XD does this.
    public override bool Enabled(RoleBehaviour? role)
    {
        return base.Enabled(role) || EffectActive;
    }

    /// <inheritdoc />
    public override PlayerControl? GetTarget()
    {
        // Hagrid doesn't know teams: anyone living fits under the cloak, impostors included - but
        // not someone already carried (devoured or already hidden).
        return PlayerControl.LocalPlayer.GetClosestLivingPlayer(true, Distance, false,
            x => !x.HasModifier<CarriedModifier>());
    }

    public override bool CanUse()
    {
        if (HudManager.Instance.Chat.IsOpenOrOpening || MeetingHud.Instance)
        {
            return false;
        }

        // While hiding, the button stays lit so it can be pressed to release early.
        if (EffectActive)
        {
            return !PlayerControl.LocalPlayer.HasDied() &&
                   !PlayerControl.LocalPlayer.HasModifier<GlitchHackedModifier>() &&
                   !PlayerControl.LocalPlayer.GetModifiers<DisabledModifier>().Any(x => !x.CanUseAbilities);
        }

        return base.CanUse() && Timer <= 0f && Target != null;
    }

    public override void ClickHandler()
    {
        if (Time.time - lastToggleTime < ToggleDebounce)
        {
            return;
        }

        // Release early: cancel the effect, which ends it via OnEffectEnd and starts the cooldown.
        if (EffectActive)
        {
            if (!CanUse())
            {
                return;
            }

            ResetCooldownAndOrEffect();
            lastToggleTime = Time.time;
            return;
        }

        // Hide: the base targeted ClickHandler owns the CanClick/uses/hacked/disabled gates, calls
        // OnClick, decrements a use, then sets EffectActive + Timer = EffectDuration (the countdown).
        base.ClickHandler();
        if (EffectActive)
        {
            lastToggleTime = Time.time;
        }
    }

    // Hide only - the release path runs through OnEffectEnd, never here.
    protected override void OnClick()
    {
        if (Target == null)
        {
            return;
        }

        Target.RpcAddModifier<CloakHiddenModifier>(PlayerControl.LocalPlayer);
        hiddenPlayer = Target;
        hideTime = Time.time;
        OverrideName(ReleaseLabel);
    }

    // The terminal release - reached both when the hide duration times out and when a mid-hide press
    // cancels the effect. Idempotent, so it's safe when a meeting/death already popped the player out.
    public override void OnEffectEnd()
    {
        if (hiddenPlayer != null && hiddenPlayer.HasModifier<CloakHiddenModifier>())
        {
            hiddenPlayer.RpcRemoveModifier<CloakHiddenModifier>();
        }

        hiddenPlayer = null;
        OverrideName(HideLabel);
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        base.FixedUpdate(playerControl);

        // If the player popped out from under the effect (their own death, or the cloak's
        // OnMeetingStart / Hagrid-death event released them) while our effect is still running, end the
        // effect so the countdown stops and the cooldown starts. Sync-settle grace keeps the brief
        // post-hide window (modifier not yet visible via HasModifier) from tripping it.
        if (EffectActive && hiddenPlayer != null &&
            (hiddenPlayer.HasDied() ||
             (Time.time - hideTime > SyncSettleTime && !hiddenPlayer.HasModifier<CloakHiddenModifier>())))
        {
            ResetCooldownAndOrEffect();
        }

        OverrideName(EffectActive ? ReleaseLabel : HideLabel);
    }
}
