using Il2CppInterop.Runtime.Attributes;
using MiraAPI.GameOptions;
using MiraAPI.Modifiers;
using MiraAPI.Modifiers.Types;
using SuperSquadAmongUs.Assets;
using SuperSquadAmongUs.Options.Modifiers;
using TownOfUs.Interfaces;
using TownOfUs.Modifiers.Game;
using TownOfUs.Modules.Localization;
using TownOfUs.Modules.Wiki;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Modifiers;

/// <summary>
/// Universal game modifier that grants its holder the Slide Tackle ability button (see
/// <see cref="Buttons.Modifiers.SlideTackleButton"/>): lunge at the nearest player and stun them with
/// <see cref="TackledModifier"/>. Assignment amount/chance live in
/// <see cref="SuperSquadModifierOptions"/>, ability tuning in
/// <see cref="Options.Modifiers.SlideTackleOptions"/>.
/// </summary>
public sealed class SlideTackleModifier : UniversalGameModifier, IWikiDiscoverable, IButtonModifier
{
    /// <inheritdoc />
    public override string LocaleKey => "SlideTackle";

    /// <inheritdoc />
    public override string ModifierName => TouLocale.Get($"SuperSquadModifier{LocaleKey}");

    /// <inheritdoc />
    public override Color FreeplayFileColor => new Color32(180, 180, 180, 255);

    /// <inheritdoc />
    public override ModifierFaction FactionType => ModifierFaction.UniversalUtility;

    /// <inheritdoc />
    public override string GetDescription()
    {
        return TouLocale.GetParsed($"SuperSquadModifier{LocaleKey}TabDescription");
    }

    /// <inheritdoc />
    public string GetAdvancedDescription()
    {
        return TouLocale.GetParsed($"SuperSquadModifier{LocaleKey}WikiDescription") +
               MiscUtils.AppendOptionsText(GetType());
    }

    /// <inheritdoc />
    [HideFromIl2Cpp]
    public List<CustomButtonWikiDescription> Abilities
    {
        get
        {
            return new List<CustomButtonWikiDescription>
            {
                new(TouLocale.Get($"SuperSquadModifier{LocaleKey}Button"),
                    TouLocale.GetParsed($"SuperSquadModifier{LocaleKey}ButtonWikiDescription"),
                    SuperSquadAssets.NeutralPlaceholderButton),
            };
        }
    }

    /// <inheritdoc />
    public override int GetAmountPerGame()
    {
        return (int)OptionGroupSingleton<SuperSquadModifierOptions>.Instance.SlideTackleAmount;
    }

    /// <inheritdoc />
    public override int GetAssignmentChance()
    {
        return (int)OptionGroupSingleton<SuperSquadModifierOptions>.Instance.SlideTackleChance;
    }

    /// <inheritdoc />
    public override bool IsModifierValidOn(RoleBehaviour role)
    {
        // Same exclusion Button Barry uses: at most one button-granting game modifier per player, so
        // the BottomLeft modifier-button slot never collides.
        return base.IsModifierValidOn(role) &&
               !role.Player.GetModifierComponent().HasModifier<GameModifier>(true, x => x is IButtonModifier);
    }
}
