using Il2CppInterop.Runtime.Attributes;
using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Modules;
using TownOfUs.Extensions;
using TownOfUs.Modules;
using TownOfUs.Modules.Localization;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Roles.Crewmate;

/// <summary>
/// Passive Crewmate Support role: invisible to every living player whenever none of them currently has
/// line of sight to him. See docs/roles/invisible-boy.md.
/// </summary>
public sealed class InvisibleBoyRole(IntPtr cppPtr) : CrewmateRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IDoomable
{
    // Anti-flicker at sight boundaries (so a single-frame sightline doesn't yank him visible and
    // straight back invisible) and an RPC throttle. The reveal itself is deliberately instant - the
    // sliver rule - since a delayed reveal would let him linger invisible right in front of a watcher.
    private const float InvisibilityGraceSeconds = 0.5f;

    private float unseenTime;

    /// <summary>
    /// Evaluates whether anyone can currently see this Invisible Boy and locally applies or clears his
    /// invisibility. Driven per-player, per-client by <see cref="Patches.InvisibleBoyVisibilityPatch"/>
    /// rather than the role's own tick (which only reliably runs for the local player): every observer
    /// computes the same answer from the shared player positions and applies the modifier locally, with
    /// no RPC - so it works for players this client doesn't own (dummies, and everyone else's Invisible
    /// Boy) too. The reveal is instant; going invisible waits out a short unseen grace.
    /// </summary>
    /// <param name="player">The Invisible Boy this role belongs to (passed from the driving patch).</param>
    public void UpdateVisibilityState(PlayerControl player)
    {
        if (!player || player.Data == null)
        {
            return;
        }

        if (player.HasDied() || MeetingHud.Instance || ExileController.Instance)
        {
            unseenTime = 0f;
            return;
        }

        var seen = SightChecker.CanAnyoneSee(player);
        var invisible = player.HasModifier<InvisibleBoyModifier>();

        if (seen)
        {
            unseenTime = 0f;
            if (invisible)
            {
                player.RemoveModifier<InvisibleBoyModifier>();
            }

            return;
        }

        if (invisible)
        {
            return;
        }

        unseenTime += Time.fixedDeltaTime;
        if (unseenTime >= InvisibilityGraceSeconds)
        {
            player.AddModifier<InvisibleBoyModifier>();
        }
    }

    public DoomableType DoomHintType => DoomableType.Insight;
    public string LocaleKey => "InvisibleBoy";
    public string RoleName => TouLocale.Get($"SuperSquadRole{LocaleKey}");
    public string RoleDescription => TouLocale.GetParsed($"SuperSquadRole{LocaleKey}IntroBlurb");
    public string RoleLongDescription => TouLocale.GetParsed($"SuperSquadRole{LocaleKey}TabDescription");

    public string GetAdvancedDescription()
    {
        return
            TouLocale.GetParsed($"SuperSquadRole{LocaleKey}WikiDescription") +
            MiscUtils.AppendOptionsText(GetType());
    }

    [HideFromIl2Cpp]
    public List<CustomButtonWikiDescription> Abilities
    {
        get
        {
            return new List<CustomButtonWikiDescription>
            {
                // No button for this ability - it's passive, so the role's own wiki sprite doubles as
                // the ability icon.
                new(TouLocale.GetParsed($"SuperSquadRole{LocaleKey}Passive", "Unseen"),
                    TouLocale.GetParsed($"SuperSquadRole{LocaleKey}PassiveWikiDescription"),
                    SuperSquadRoleIcons.InvisibleBoy),
            };
        }
    }

    public Color RoleColor => SuperSquadColors.InvisibleBoy;
    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;
    public RoleAlignment RoleAlignment => RoleAlignment.CrewmateSupport;

    public CustomRoleConfiguration Configuration => new(this)
    {
        Icon = SuperSquadRoleIcons.InvisibleBoy,
        OptionsScreenshot = SuperSquadAssets.Banner,
    };
}
