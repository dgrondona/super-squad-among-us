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
/// The mafia's leader; a plain impostor whose being alive gates the Mafioso; ported from TOR (docs/porting/tor-mafia.md).
/// </summary>
public sealed class GodfatherRole(IntPtr cppPtr) : ImpostorRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IDoomable
{
    public DoomableType DoomHintType => DoomableType.Death;
    public string IdPart => "Godfather";
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

    // Spawns only as part of the mafia trio (MafiaAssignmentPatch), never on its own - drafting one
    // in isolation would leave a Godfather with no Mafioso to gate. Same reason TOU-Mira opts
    // TraitorRole/PestilenceRole out of the draft pool.
    public bool IsDraftable => false;

    public CustomRoleConfiguration Configuration => new(this)
    {
        UseVanillaKillButton = true,
        CanUseVent = true,
        CanUseSabotage = true,
        IntroSound = TouAudio.GlitchSound,
        DefaultRoleCount = 0,
        DefaultChance = 0,
        HideSettings = true,
    };
}
