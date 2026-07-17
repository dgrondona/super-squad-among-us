using AmongUs.GameOptions;
using Il2CppInterop.Runtime.Attributes;
using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modifiers;
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

public sealed class PelicanRole(IntPtr cppPtr) : NeutralRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IDoomable
{
    public DoomableType DoomHintType => DoomableType.Relentless;
    public string LocaleKey => "Pelican";
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
                new(TouLocale.GetParsed($"SuperSquadRole{LocaleKey}Devour", "Devour"),
                    TouLocale.GetParsed($"SuperSquadRole{LocaleKey}DevourWikiDescription"),
                    SuperSquadNeutAssets.PelicanDevourSprite),
            };
        }
    }

    public Color RoleColor => SuperSquadColors.Pelican;
    public ModdedRoleTeams Team => ModdedRoleTeams.Custom;
    public RoleAlignment RoleAlignment => RoleAlignment.NeutralKilling;

    public CustomRoleConfiguration Configuration => new(this)
    {
        CanUseVent = OptionGroupSingleton<PelicanOptions>.Instance.CanVent,
        IntroSound = TouAudio.GlitchSound,
        // Cover icon intentionally unset (user request 2026-07-16) until real role art exists.
        GhostRole = (RoleTypes)RoleId.Get<NeutralGhostRole>(),
    };

    public bool HasImpostorVision => OptionGroupSingleton<PelicanOptions>.Instance.ImpostorVision;

    /// <summary>
    /// Arsonist-format neutral win (user decision, see docs/porting/README.md), with one twist:
    /// devoured players are as-good-as-dead (they die at the next meeting and can't act), so they
    /// count neither as alive nor as living killers - otherwise a full stomach (or a devoured rival
    /// killer) could soft-lock the round.
    /// </summary>
    /// <returns>True if the Pelican wins alongside game end.</returns>
    public bool WinConditionMet()
    {
        if (Player.HasDied())
        {
            return false;
        }

        var aliveNotDevoured = Helpers.GetAlivePlayers().Count(x => !x.HasModifier<DevouredModifier>());

        // KillersAliveCount includes devoured players (they're technically alive), so subtract the
        // impostor/neutral-killer ones sitting in a stomach. (A devoured power-crew killer with
        // CrewKillersContinue on would still count - rare enough to accept.)
        var devouredKillers = ModifierUtils.GetActiveModifiers<DevouredModifier>()
            .Select(x => x.Player)
            .Count(x => x != null && !x.HasDied() &&
                        (x.IsImpostor() || x.Is(RoleAlignment.NeutralKilling)));

        return aliveNotDevoured <= 2 && MiscUtils.KillersAliveCount - devouredKillers == 1;
    }

    public override bool DidWin(GameOverReason gameOverReason)
    {
        return WinConditionMet();
    }
}
