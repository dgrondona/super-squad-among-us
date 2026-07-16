using AmongUs.GameOptions;
using Il2CppInterop.Runtime.Attributes;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using SuperSquadAmongUs.Assets;
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

public sealed class VultureRole(IntPtr cppPtr) : NeutralRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IDoomable
{
    public DoomableType DoomHintType => DoomableType.Relentless;
    public string LocaleKey => "Vulture";
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
                new(TouLocale.GetParsed($"SuperSquadRole{LocaleKey}Eat", "Eat"),
                    TouLocale.GetParsed($"SuperSquadRole{LocaleKey}EatWikiDescription"),
                    SuperSquadNeutAssets.VultureEatSprite),
            };
        }
    }

    public Color RoleColor => SuperSquadColors.Vulture;
    public ModdedRoleTeams Team => ModdedRoleTeams.Custom;
    public RoleAlignment RoleAlignment => RoleAlignment.NeutralEvil;

    public CustomRoleConfiguration Configuration => new(this)
    {
        CanUseVent = OptionGroupSingleton<VultureOptions>.Instance.CanVent,
        IntroSound = TouAudio.GlitchSound,
        Icon = SuperSquadRoleIcons.Vulture,
        GhostRole = (RoleTypes)RoleId.Get<NeutralGhostRole>(),
    };

    /// <summary>
    /// Incremented on every client by SuperSquadBodies.RpcVultureEat.
    /// </summary>
    public int EatenBodies { get; set; }

    /// <summary>
    /// Arsonist-format neutral win (user decision, see docs/porting/README.md): TOU-Mira's NeutralRoleWinCondition polls this every frame, so the win ends the game instantly, TOR-style.
    /// </summary>
    /// <returns>True if the Vulture wins alongside game end.</returns>
    public bool WinConditionMet()
    {
        return !Player.HasDied() && EatenBodies >= (int)OptionGroupSingleton<VultureOptions>.Instance.BodiesNeededToWin;
    }

    public override bool DidWin(GameOverReason gameOverReason)
    {
        return WinConditionMet();
    }
}
