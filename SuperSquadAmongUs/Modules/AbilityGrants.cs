using MiraAPI.Roles;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// Abilities Gooper/Kirby can dynamically gain beyond their base role, beyond the escalation every
/// role already gets natively. See docs/roles/gooper.md and docs/roles/kirby.md for the design
/// rationale: this is a curated, extensible vocabulary, not a generic "clone any role" engine - no
/// primitive for that exists in MiraAPI/TOU-Mira short of a full role swap (<c>ChangeRole</c>).
/// </summary>
[Flags]
public enum GrantableAbility : uint
{
    None = 0,
    Kill = 1,
    Vent = 2,
    Snipe = 4,
    Swoop = 8,
    Vest = 16,
    Hide = 32,
}

/// <summary>
/// Implemented by any role whose kit can grow at runtime via <see cref="GrantableAbility"/> flags
/// (Gooper, Kirby) rather than a full role swap. The property is plain mutable state, not a modifier -
/// it's set directly inside an already-deterministic RPC/event handler on every client, the same way
/// <c>VultureRole.EatenBodies</c> is incremented in <see cref="SuperSquadBodies.RpcVultureEat"/>.
/// </summary>
public interface IAbilityGrantHolder
{
    GrantableAbility UnlockedAbilities { get; set; }
}

/// <summary>
/// The shared "grow a role's kit at runtime" machinery both Gooper and Kirby build on.
/// </summary>
public static class AbilityGrants
{
    /// <summary>
    /// Abilities Gooper draws from at random (without replacement) starting with its 3rd goop.
    /// Puppeteer's control ability is deliberately not in this pool yet - it's a whole remote-control
    /// subsystem in TOU-Mira (per-client control state, a movement-hijacking Harmony patch, camera/
    /// light sync, disconnect handling), far bigger than the rest of this pool combined. Add it here
    /// (plus a <see cref="GrantableAbility"/> flag and a matching button/modifier pair) once that cost
    /// is separately scoped - see the "Puppet Master scope" decision in docs/roles/gooper.md.
    /// </summary>
    public static readonly GrantableAbility[] Pool =
        [GrantableAbility.Snipe, GrantableAbility.Swoop, GrantableAbility.Hide];

    /// <summary>
    /// Picks one ability from <see cref="Pool"/> that <paramref name="alreadyUnlocked"/> doesn't have
    /// yet, without replacement (confirmed design decision - repeats would waste a goop). Returns
    /// <see cref="GrantableAbility.None"/> once the pool is exhausted.
    /// </summary>
    /// <remarks>
    /// Must be called on exactly one client (the acting Gooper, before sending the goop RPC) and the
    /// result passed through as an RPC argument so every client applies the identical draw - the same
    /// split <see cref="SuperSquadAmongUs.Events.ElusiveEvents"/> uses for its teleport destination.
    /// </remarks>
    public static GrantableAbility PickRandomPoolAbility(GrantableAbility alreadyUnlocked)
    {
        var remaining = Pool.Where(a => !alreadyUnlocked.HasFlag(a)).ToList();
        return remaining.Count == 0 ? GrantableAbility.None : remaining[UnityEngine.Random.Range(0, remaining.Count)];
    }

    /// <summary>
    /// Turns on venting for a role that just unlocked <see cref="GrantableAbility.Vent"/> mid-game.
    /// </summary>
    /// <remarks>
    /// Two things must happen. (1) <c>Configuration.CanUseVent</c> already reads the Vent flag live (it
    /// drives <c>Vent.CanUse</c>), but the vanilla <c>RoleBehaviour.CanVent</c> bool is baked once at
    /// role setup by MiraAPI's <c>CustomRoleManager</c> - set it so every cached-bool vent path agrees.
    /// (2) The on-screen <c>ImpostorVentButton</c>'s visibility is only applied inside
    /// <c>HudManager.SetHudActive</c> (a MiraAPI postfix), which is NOT re-run per frame - so re-run it
    /// on the owner's own client to surface the vent button the instant venting is unlocked.
    /// </remarks>
    /// <param name="role">The role that just gained the Vent flag.</param>
    public static void EnableVenting(RoleBehaviour role)
    {
        role.CanVent = true;

        if (role.Player != null && role.Player.AmOwner && HudManager.InstanceExists)
        {
            HudManager.Instance.SetHudActive(PlayerControl.LocalPlayer, role, true);
        }
    }

    /// <summary>
    /// What Kirby inherits from a digested victim's role. Deliberately a small, hand-maintained lookup,
    /// not a reflection-based clone of the victim's actual behavior - grow this table as more roles
    /// become worth inheriting from, rather than trying to cover every role up front.
    /// </summary>
    /// <param name="victimRole">The digested player's role at the moment of digestion.</param>
    /// <returns>The flags Kirby should OR into its own <see cref="IAbilityGrantHolder.UnlockedAbilities"/>.</returns>
    public static GrantableAbility GetPortableAbilities(RoleBehaviour? victimRole)
    {
        if (victimRole == null)
        {
            return GrantableAbility.None;
        }

        // A digested Gooper/Kirby hands over everything it had already unlocked, wholesale.
        if (victimRole is IAbilityGrantHolder holder)
        {
            return holder.UnlockedAbilities;
        }

        var abilities = GrantableAbility.None;

        if (victimRole.CanVent)
        {
            abilities |= GrantableAbility.Vent;
        }

        // Roles that keep the vanilla kill button (most Impostor and Neutral Killing roles in this
        // addon) count as kill-capable. Vanilla base-game roles with no ICustomRole configuration
        // aren't covered by this check - a known, accepted gap (see docs/roles/kirby.md).
        if (victimRole is ICustomRole { Configuration.UseVanillaKillButton: true })
        {
            abilities |= GrantableAbility.Kill;
        }

        if (victimRole is SuperSquadAmongUs.Roles.Impostor.SniperRole)
        {
            abilities |= GrantableAbility.Snipe;
        }

        if (victimRole is TownOfUs.Roles.Impostor.SwooperRole)
        {
            abilities |= GrantableAbility.Swoop;
        }

        if (victimRole is SuperSquadAmongUs.Roles.Crewmate.DaddyHagridRole)
        {
            abilities |= GrantableAbility.Hide;
        }

        return abilities;
    }
}
