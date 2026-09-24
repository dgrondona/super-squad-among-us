using Il2CppInterop.Runtime.Attributes;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using MiraAPI.Translation;
using TownOfUs;
using TownOfUs.Assets;
using TownOfUs.Extensions;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Roles.Impostor;

/// <summary>
/// Cannot kill or sabotage while a living Godfather exists (gates live in Patches/MafiosoGatePatches.cs); unlocks immediately when the Godfather dies, cooldown not reset (TOR behavior).
/// </summary>
public sealed class MafiosoRole(IntPtr cppPtr) : ImpostorRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IDoomable
{
    public DoomableType DoomHintType => DoomableType.Death;
    public string IdPart => "Mafioso";
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
            return new List<CustomButtonWikiDescription>();
        }
    }

    public Color RoleColor => TownOfUsColors.Impostor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Impostor;
    public RoleAlignment RoleAlignment => RoleAlignment.ImpostorKilling;

    // Spawns only as part of the mafia trio (MafiaAssignmentPatch) - a drafted Mafioso with no
    // Godfather would be permanently gated by MafiosoGatePatches. See GodfatherRole.
    public bool IsDraftable => false;

    public CustomRoleConfiguration Configuration => new(this)
    {
        UseVanillaKillButton = true,
        CanUseVent = true,
        CanUseSabotage = true,
        DefaultRoleCount = 0,
        DefaultChance = 0,
        HideSettings = true,
    };
}
