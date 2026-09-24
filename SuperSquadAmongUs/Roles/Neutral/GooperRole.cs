using AmongUs.GameOptions;
using Il2CppInterop.Runtime.Attributes;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using MiraAPI.Translation;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modules;
using TownOfUs;
using TownOfUs.Assets;
using TownOfUs.Extensions;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Roles.Neutral;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Roles.Neutral;

public sealed class GooperRole(IntPtr cppPtr) : NeutralRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IDoomable, IAbilityGrantHolder
{
    /// <summary>
    /// Dead bodies already gooped, by <see cref="DeadBody.ParentId"/> - a body can only be gooped once.
    /// </summary>
    [HideFromIl2Cpp]
    public HashSet<byte> GoopedBodyIds { get; } = [];

    /// <inheritdoc />
    public GrantableAbility UnlockedAbilities { get; set; }

    /// <inheritdoc />
    [HideFromIl2Cpp]
    public HashSet<Type> GrantedKits { get; } = [];

    /// <inheritdoc />
    public bool BaseCanVent => false;

    public DoomableType DoomHintType => DoomableType.Relentless;
    public string IdPart => "Gooper";
    public string RoleName => MiraLocaleManager.Get($"SuperSquadRole{IdPart}");
    public string RoleDescription => MiraLocaleManager.Get($"SuperSquadRole{IdPart}IntroBlurb");
    public string RoleLongDescription => MiraLocaleManager.Get($"SuperSquadRole{IdPart}TabDescription");

    public string GetAdvancedDescription()
    {
        return
            MiraLocaleManager.Get($"SuperSquadRole{IdPart}WikiDescription") +
            MiscUtils.AppendOptionsText(GetType());
    }

    [HideFromIl2Cpp]
    public List<CustomButtonWikiDescription> Abilities
    {
        get
        {
            return new List<CustomButtonWikiDescription>
            {
                new(MiraLocaleManager.Get($"SuperSquadRole{IdPart}Goop", "Goop"),
                    MiraLocaleManager.Get($"SuperSquadRole{IdPart}GoopWikiDescription"),
                    SuperSquadAssets.NeutralPlaceholderButton),
            };
        }
    }

    public Color RoleColor => SuperSquadColors.Gooper;
    public ModdedRoleTeams Team => ModdedRoleTeams.Custom;
    public RoleAlignment RoleAlignment => RoleAlignment.NeutralKilling;

    public CustomRoleConfiguration Configuration => new(this)
    {
        CanUseVent = UnlockedAbilities.HasFlag(GrantableAbility.Vent),
        IntroSound = TouAudio.GlitchSound,
        Icon = SuperSquadAssets.NeutralPlaceholderIcon,
        GhostRole = (RoleTypes)RoleId.Get<NeutralGhostRole>(),
    };

    /// <summary>
    /// Last-one-standing Neutral Killing win, matching <c>SentinelRole</c> - confirmed design decision
    /// (Gooper eventually gains a permanent kill button, so a Vulture-style instant threshold win on
    /// goop count doesn't fit).
    /// </summary>
    /// <returns>True if the Gooper wins alongside game end.</returns>
    public bool WinConditionMet()
    {
        var goopersAlive = CustomRoleUtils.GetActiveRolesOfType<GooperRole>().Count(x => !x.Player.HasDied());

        if (MiscUtils.KillersAliveCount > goopersAlive)
        {
            return false;
        }

        return goopersAlive >= Helpers.GetAlivePlayers().Count - goopersAlive;
    }

    public override bool DidWin(GameOverReason gameOverReason)
    {
        return WinConditionMet();
    }
}
