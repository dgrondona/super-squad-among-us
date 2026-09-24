using Il2CppInterop.Runtime.Attributes;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using MiraAPI.Translation;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Options.Roles.Impostor;
using TownOfUs;
using TownOfUs.Assets;
using TownOfUs.Extensions;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Roles.Impostor;

/// <summary>
/// The mafia's cleaner; no kill button, cleans one body per cooldown; distinct from TOU-Mira's standalone Janitor role.
/// </summary>
public sealed class MafiaJanitorRole(IntPtr cppPtr) : ImpostorRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IDoomable
{
    public DoomableType DoomHintType => DoomableType.Trickster;
    public string IdPart => "MafiaJanitor";
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
                new(MiraLocaleManager.Get($"SuperSquadRole{IdPart}Clean", "Clean"),
                    MiraLocaleManager.Get($"SuperSquadRole{IdPart}CleanWikiDescription"),
                    SuperSquadImpAssets.MafiaJanitorCleanSprite),
            };
        }
    }

    public Color RoleColor => TownOfUsColors.Impostor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Impostor;
    public RoleAlignment RoleAlignment => RoleAlignment.ImpostorSupport;

    // Spawns only as part of the mafia trio (MafiaAssignmentPatch), never on its own. See GodfatherRole.
    public bool IsDraftable => false;

    public CustomRoleConfiguration Configuration => new(this)
    {
        UseVanillaKillButton = false,
        CanUseVent = true,
        CanUseSabotage = true,
        DefaultRoleCount = 0,
        DefaultChance = 0,
        HideSettings = true,
    };
}
