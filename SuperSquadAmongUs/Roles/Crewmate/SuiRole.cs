using Il2CppInterop.Runtime.Attributes;
using MiraAPI.Modifiers;
using MiraAPI.Roles;
using MiraAPI.Translation;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Modifiers;
using TownOfUs;
using TownOfUs.Assets;
using TownOfUs.Extensions;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Roles.Crewmate;

public sealed class SuiRole(IntPtr cppPtr) : CrewmateRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IDoomable
{
    // The "currently protected player" mirror lives on SuiProtectButton, not here: buttons are
    // per-client singletons, so keeping the state there lets a role that borrowed the Sui kit
    // (AbilityGrants.GrantedKits) share it. The synced state of record is SuiProtectedModifier.
    public DoomableType DoomHintType => DoomableType.Protective;
    public string IdPart => "Sui";
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
                new(MiraLocaleManager.Get($"SuperSquadRole{IdPart}Protect", "Protect"),
                    MiraLocaleManager.Get($"SuperSquadRole{IdPart}ProtectWikiDescription"),
                    SuperSquadAssets.CrewmatePlaceholderButton),
                new(MiraLocaleManager.Get($"SuperSquadRole{IdPart}Retaliate", "Retaliate"),
                    MiraLocaleManager.Get($"SuperSquadRole{IdPart}RetaliateWikiDescription"),
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
