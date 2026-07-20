using AmongUs.GameOptions;
using Il2CppInterop.Runtime.Attributes;
using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Modules;
using SuperSquadAmongUs.Options.Roles.Neutral;
using TownOfUs;
using TownOfUs.Assets;
using TownOfUs.Extensions;
using TownOfUs.Modules.Localization;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Roles.Neutral;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Roles.Neutral;

public sealed class KirbyRole(IntPtr cppPtr) : NeutralRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IDoomable, IAbilityGrantHolder
{
    /// <inheritdoc />
    public GrantableAbility UnlockedAbilities { get; set; }

    public DoomableType DoomHintType => DoomableType.Relentless;
    public string LocaleKey => "Kirby";
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
                new(TouLocale.GetParsed($"SuperSquadRole{LocaleKey}Swallow", "Swallow"),
                    TouLocale.GetParsed($"SuperSquadRole{LocaleKey}SwallowWikiDescription"),
                    SuperSquadAssets.NeutralPlaceholderButton),
            };
        }
    }

    public Color RoleColor => SuperSquadColors.Kirby;
    public ModdedRoleTeams Team => ModdedRoleTeams.Custom;
    public RoleAlignment RoleAlignment => RoleAlignment.NeutralKilling;

    public CustomRoleConfiguration Configuration => new(this)
    {
        CanUseVent = OptionGroupSingleton<KirbyOptions>.Instance.CanVent || UnlockedAbilities.HasFlag(GrantableAbility.Vent),
        IntroSound = TouAudio.GlitchSound,
        Icon = SuperSquadAssets.NeutralPlaceholderIcon,
        GhostRole = (RoleTypes)RoleId.Get<NeutralGhostRole>(),
    };

    public bool HasImpostorVision => OptionGroupSingleton<KirbyOptions>.Instance.ImpostorVision;

    /// <summary>
    /// Last-one-standing Neutral Killing win, matching <c>SentinelRole</c>/<c>GooperRole</c> - same
    /// archetype as Gooper's confirmed win-condition decision. Swallowed-but-not-yet-digested players
    /// are excluded from both counts, matching <c>PelicanRole.WinConditionMet</c>'s soft-lock fix.
    /// </summary>
    /// <returns>True if Kirby wins alongside game end.</returns>
    public bool WinConditionMet()
    {
        if (Player.HasDied())
        {
            return false;
        }

        var kirbysAlive = CustomRoleUtils.GetActiveRolesOfType<KirbyRole>().Count(x => !x.Player.HasDied());

        // Swallowed-but-undigested players are as-good-as-dead (they die at the next meeting and
        // can't act), so - same reasoning as PelicanRole.WinConditionMet - they count neither as
        // alive nor as living killers, or a full stomach (or a swallowed rival killer) could
        // soft-lock the round.
        var swallowedKillers = ModifierUtils.GetActiveModifiers<KirbySwallowedModifier>()
            .Select(x => x.Player)
            .Count(x => x != null && !x.HasDied() &&
                        (x.IsImpostor() || x.Is(RoleAlignment.NeutralKilling)));

        if (MiscUtils.KillersAliveCount - swallowedKillers > kirbysAlive)
        {
            return false;
        }

        var aliveNotSwallowed = Helpers.GetAlivePlayers().Count(x => !x.HasModifier<KirbySwallowedModifier>());

        return kirbysAlive >= aliveNotSwallowed - kirbysAlive;
    }

    public override bool DidWin(GameOverReason gameOverReason)
    {
        return WinConditionMet();
    }
}
