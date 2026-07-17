using Il2CppInterop.Runtime.Attributes;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using SuperSquadAmongUs.Assets;
using TownOfUs.Extensions;
using TownOfUs.Modules;
using TownOfUs.Modules.Localization;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Roles.Crewmate;

public sealed class DaddyHagridRole(IntPtr cppPtr) : CrewmateRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IDoomable
{
    public DoomableType DoomHintType => DoomableType.Protective;
    public string LocaleKey => "DaddyHagrid";
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
                new(TouLocale.GetParsed($"SuperSquadRole{LocaleKey}Hide", "Hide"),
                    TouLocale.GetParsed($"SuperSquadRole{LocaleKey}HideWikiDescription"),
                    SuperSquadAssets.CrewmatePlaceholderButton),
            };
        }
    }

    public Color RoleColor => SuperSquadColors.DaddyHagrid;
    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;
    public RoleAlignment RoleAlignment => RoleAlignment.CrewmateProtective;

    public CustomRoleConfiguration Configuration => new(this)
    {
        Icon = SuperSquadRoleIcons.DaddyHagrid,
        OptionsScreenshot = SuperSquadAssets.Banner,
    };
}
