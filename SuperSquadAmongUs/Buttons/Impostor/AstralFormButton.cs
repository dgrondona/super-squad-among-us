using MiraAPI.GameOptions;
using MiraAPI.Keybinds;
using MiraAPI.Modifiers;
using MiraAPI.Utilities.Assets;
using SuperSquadAmongUs.Modifiers;
using SuperSquadAmongUs.Options.Roles.Impostor;
using SuperSquadAmongUs.Roles.Impostor;
using TownOfUs;
using TownOfUs.Assets;
using TownOfUs.Buttons;
using TownOfUs.Modules.Localization;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons.Impostor;

public sealed class AstralFormButton : TownOfUsRoleButton<AstralRole>
{
    public override string Name => TouLocale.GetParsed("SuperSquadRoleAstralForm", "Phase");
    public override BaseKeybind Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => TownOfUsColors.Impostor;
    public override float Cooldown => Math.Clamp(OptionGroupSingleton<AstralOptions>.Instance.FormCooldown + MapCooldown, 5f, 120f);

    // The button's "effect" spans both phases (ghost form + invisibility linger) so the cooldown only
    // starts once the Astral is fully visible again. The modifiers time themselves out independently.
    public override float EffectDuration =>
        OptionGroupSingleton<AstralOptions>.Instance.FormDuration +
        OptionGroupSingleton<AstralOptions>.Instance.LingerDuration;

    // Reuses TOU-Mira's TimeLord Rewind button sprite until this role gets real button art.
    public override LoadableAsset<Sprite> Sprite => TouCrewAssets.RewindSprite;

    public override bool CanUse()
    {
        if (HudManager.Instance.Chat.IsOpenOrOpening || MeetingHud.Instance)
        {
            return false;
        }

        // The form cannot be cancelled early - once phased, the tether runs its course.
        return base.CanUse() && !EffectActive;
    }

    protected override void OnClick()
    {
        PlayerControl.LocalPlayer.RpcAddModifier<AstralFormModifier>();
    }

    public override void OnEffectEnd()
    {
        // The modifiers remove themselves on their own timers (or on meeting/death); this only cleans
        // up leftovers if the button timer and modifier timers ever disagree.
        if (PlayerControl.LocalPlayer.HasModifier<AstralFormModifier>())
        {
            PlayerControl.LocalPlayer.RpcRemoveModifier<AstralFormModifier>();
        }

        if (PlayerControl.LocalPlayer.HasModifier<AstralLingerModifier>())
        {
            PlayerControl.LocalPlayer.RpcRemoveModifier<AstralLingerModifier>();
        }
    }
}
