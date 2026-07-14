using Il2CppInterop.Runtime.Attributes;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using TouExtensionExample.Assets;
using TownOfUs.Extensions;
using TownOfUs.Modules;
using TownOfUs.Modules.Localization;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace TouExtensionExample.Roles.Crewmate;

public sealed class ApparaterRole(IntPtr cppPtr) : CrewmateRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IDoomable
{
    public DoomableType DoomHintType => DoomableType.Trickster;
    public string LocaleKey => "Apparater";
    public string RoleName => TouLocale.Get($"ExampleRole{LocaleKey}");
    public string RoleDescription => TouLocale.GetParsed($"ExampleRole{LocaleKey}IntroBlurb");
    public string RoleLongDescription => TouLocale.GetParsed($"ExampleRole{LocaleKey}TabDescription");

    public string GetAdvancedDescription()
    {
        return
            TouLocale.GetParsed($"ExampleRole{LocaleKey}WikiDescription") +
            MiscUtils.AppendOptionsText(GetType());
    }

    [HideFromIl2Cpp]
    public List<CustomButtonWikiDescription> Abilities
    {
        get
        {
            return new List<CustomButtonWikiDescription>
            {
                new(TouLocale.GetParsed($"ExampleRole{LocaleKey}Teleport", "Teleport"),
                    TouLocale.GetParsed($"ExampleRole{LocaleKey}TeleportWikiDescription"),
                    ExampleCrewAssets.ApparaterMapSprite),
            };
        }
    }

    public Color RoleColor => TouExampleColors.Apparater;
    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;
    public RoleAlignment RoleAlignment => RoleAlignment.CrewmatePower;

    public CustomRoleConfiguration Configuration => new(this)
    {
        Icon = ExampleRoleIcons.Apparater,
        OptionsScreenshot = ExampleAssets.Banner,
    };
}
