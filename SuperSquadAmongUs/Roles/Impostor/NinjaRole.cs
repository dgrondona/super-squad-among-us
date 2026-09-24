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

public sealed class NinjaRole(IntPtr cppPtr) : ImpostorRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IDoomable
{
    public DoomableType DoomHintType => DoomableType.Death;
    public string IdPart => "Ninja";
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
                new(MiraLocaleManager.Get($"SuperSquadRole{IdPart}Mark", "Mark"),
                    MiraLocaleManager.Get($"SuperSquadRole{IdPart}MarkWikiDescription"),
                    SuperSquadImpAssets.NinjaMarkSprite),
                new(MiraLocaleManager.Get($"SuperSquadRole{IdPart}Assassinate", "Assassinate"),
                    MiraLocaleManager.Get($"SuperSquadRole{IdPart}AssassinateWikiDescription"),
                    SuperSquadImpAssets.NinjaAssassinateSprite),
            };
        }
    }

    public Color RoleColor => TownOfUsColors.Impostor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Impostor;
    public RoleAlignment RoleAlignment => RoleAlignment.ImpostorKilling;

    public CustomRoleConfiguration Configuration => new(this)
    {
        CanUseVent = OptionGroupSingleton<NinjaOptions>.Instance.CanVent,
        IntroSound = TouAudio.PhantomIntroSound,
    };
}
