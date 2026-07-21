using MiraAPI.PluginLoading;
using Reactor.Utilities.Extensions;
using SuperSquadAmongUs.Modules;
using TownOfUs.Buttons;
using TownOfUs.Extensions;
using TownOfUs.Utilities;
using UnityEngine;

namespace SuperSquadAmongUs.Buttons;

// The grant-aware role-button bases every role button in this addon extends instead of TOU-Mira's
// TownOfUsRoleButton<TRole> directly. The ONLY behavioral difference: Enabled also passes when the
// local role has BORROWED this role's kit (AbilityGrants.IsOrHolds) - which is what makes every
// ability in this addon automatically portable to Kirby/Gooper (and any future grant-holder) with no
// per-ability copy. The targeted variant also re-derives the outline color from the local player's
// own role, because the TOU-Mira base reads Role.TeamColor and Role is null for a borrower.
// See docs/architecture.md's "Granting a role abilities it wasn't born with".

/// <summary>
/// Grant-aware sibling of <c>TownOfUsRoleButton&lt;TRole&gt;</c>: shown when the local role IS
/// <typeparamref name="TRole"/> or has borrowed its kit.
/// </summary>
/// <typeparam name="TRole">The role this button natively belongs to.</typeparam>
[MiraIgnore]
public abstract class SuperSquadRoleButton<TRole> : TownOfUsRoleButton<TRole>
    where TRole : RoleBehaviour
{
    /// <inheritdoc />
    public override bool Enabled(RoleBehaviour? role)
    {
        return !Disabled && AbilityGrants.IsOrHolds(role, typeof(TRole));
    }
}

/// <summary>
/// Grant-aware sibling of <c>TownOfUsRoleButton&lt;TRole, TTarget&gt;</c>: shown when the local role IS
/// <typeparamref name="TRole"/> or has borrowed its kit.
/// </summary>
/// <typeparam name="TRole">The role this button natively belongs to.</typeparam>
/// <typeparam name="TTarget">The targeted scene object type.</typeparam>
[MiraIgnore]
public abstract class SuperSquadRoleButton<TRole, TTarget> : TownOfUsRoleButton<TRole, TTarget>
    where TTarget : MonoBehaviour
    where TRole : RoleBehaviour
{
    /// <inheritdoc />
    public override bool Enabled(RoleBehaviour? role)
    {
        return !Disabled && AbilityGrants.IsOrHolds(role, typeof(TRole));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Base implementation outlines with <c>Role.TeamColor</c>, which is null for a borrower; the
    /// local player's actual role color is identical for the native holder and correct for everyone.
    /// </remarks>
    public override void SetOutline(bool active)
    {
        if (Target == null || PlayerControl.LocalPlayer.HasDied())
        {
            return;
        }

        var teamColor = PlayerControl.LocalPlayer.Data.Role.TeamColor;
        if (Target is PlayerControl target)
        {
            target.cosmetics.currentBodySprite.BodySprite.SetOutline(active ? teamColor : null);
        }
        else if (Target is DeadBody body)
        {
            foreach (var renderer in body.bodyRenderers)
            {
                renderer.SetOutline(active ? teamColor : null);
            }
        }
        else if (Target is Vent vent)
        {
            vent.SetOutline(active, true, teamColor);
        }
    }
}

/// <summary>
/// Grant-aware sibling of <c>TownOfUsKillRoleButton&lt;TRole, TTarget&gt;</c> (kill-cooldown-tracking
/// targeted buttons, i.e. anything implementing <c>IKillButton</c>).
/// </summary>
/// <typeparam name="TRole">The role this button natively belongs to.</typeparam>
/// <typeparam name="TTarget">The targeted scene object type.</typeparam>
[MiraIgnore]
public abstract class SuperSquadKillRoleButton<TRole, TTarget> : SuperSquadRoleButton<TRole, TTarget>
    where TTarget : MonoBehaviour
    where TRole : RoleBehaviour
{
    /// <inheritdoc />
    protected override bool ShouldTrackKillCooldown()
    {
        return true;
    }
}
