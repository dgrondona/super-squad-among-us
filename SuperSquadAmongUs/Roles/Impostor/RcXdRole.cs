using Il2CppInterop.Runtime.Attributes;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Options.Roles.Impostor;
using TownOfUs;
using TownOfUs.Assets;
using TownOfUs.Extensions;
using TownOfUs.Modules.Localization;
using TownOfUs.Modules.Wiki;
using TownOfUs.Roles;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Roles.Impostor;

public sealed class RcXdRole(IntPtr cppPtr) : ImpostorRole(cppPtr), ITownOfUsRole, IWikiDiscoverable, IDoomable
{
    public DoomableType DoomHintType => DoomableType.Hunter;
    public string LocaleKey => "RcXd";
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
                new(TouLocale.GetParsed($"SuperSquadRole{LocaleKey}Deploy", "Deploy"),
                    TouLocale.GetParsed($"SuperSquadRole{LocaleKey}DeployWikiDescription"),
                    SuperSquadImpAssets.RcXdDeploySprite),
                new(TouLocale.GetParsed($"SuperSquadRole{LocaleKey}Detonate", "Detonate"),
                    TouLocale.GetParsed($"SuperSquadRole{LocaleKey}DetonateWikiDescription"),
                    SuperSquadImpAssets.RcXdDetonateSprite),
            };
        }
    }

    public Color RoleColor => TownOfUsColors.Impostor;
    public ModdedRoleTeams Team => ModdedRoleTeams.Impostor;
    public RoleAlignment RoleAlignment => RoleAlignment.ImpostorKilling;

    public CustomRoleConfiguration Configuration => new(this)
    {
        CanUseVent = OptionGroupSingleton<RcXdOptions>.Instance.CanVent,
        IntroSound = TouAudio.PhantomIntroSound,
    };
}
