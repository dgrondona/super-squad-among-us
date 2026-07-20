using Il2CppInterop.Runtime.Attributes;
using MiraAPI.Modifiers;
using MiraAPI.Roles;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modifiers;
using TownOfUs;
using TownOfUs.Assets;
using TownOfUs.Extensions;
using TownOfUs.Modules.Localization;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Roles.Crewmate;

public sealed class SuiRole(IntPtr cppPtr) : CrewmateRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IDoomable
{
    /// <summary>
    /// Gets or sets the player currently protected, mirrored on this client for
    /// <see cref="Buttons.Crewmate.SuiProtectButton"/>'s targeting; the synced state of record is
    /// <see cref="SuiProtectedModifier"/> on the target.
    /// </summary>
    [HideFromIl2Cpp]
    public PlayerControl? Protected { get; set; }

    public void FixedUpdate()
    {
        if (!Player || Player.Data.Role is not SuiRole)
        {
            return;
        }

        if (Protected != null && (Protected.HasDied() || !Protected.HasModifier<SuiProtectedModifier>()))
        {
            Protected = null;
        }
    }

    public DoomableType DoomHintType => DoomableType.Protective;
    public string LocaleKey => "Sui";
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
                new(TouLocale.GetParsed($"SuperSquadRole{LocaleKey}Protect", "Protect"),
                    TouLocale.GetParsed($"SuperSquadRole{LocaleKey}ProtectWikiDescription"),
                    SuperSquadAssets.CrewmatePlaceholderButton),
                new(TouLocale.GetParsed($"SuperSquadRole{LocaleKey}Retaliate", "Retaliate"),
                    TouLocale.GetParsed($"SuperSquadRole{LocaleKey}RetaliateWikiDescription"),
                    SuperSquadAssets.CrewmatePlaceholderButton),
            };
        }
    }

    public Color RoleColor => SuperSquadColors.Sui;
    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;
    public RoleAlignment RoleAlignment => RoleAlignment.CrewmateProtective;

    public CustomRoleConfiguration Configuration => new(this)
    {
        Icon = SuperSquadAssets.CrewmatePlaceholderIcon,
        OptionsScreenshot = SuperSquadAssets.Banner,
    };
}
