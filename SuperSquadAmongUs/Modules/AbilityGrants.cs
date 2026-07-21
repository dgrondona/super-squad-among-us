using MiraAPI.Roles;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// Ability primitives a role can dynamically gain beyond its base kit. These are the *non-kit* grants:
/// effects that either come from a TOU-Mira role whose buttons we can't borrow (Swoop), from vanilla
/// (Kill, Vent), or from no role at all (Gooper's Vest). Abilities sourced from this addon's own roles
/// are NOT flags - they transfer as whole button kits via <see cref="IAbilityGrantHolder.GrantedKits"/>
/// (see <c>Buttons/SuperSquadRoleButton.cs</c> and docs/architecture.md's granting section).
/// </summary>
[Flags]
public enum GrantableAbility : uint
{
    None = 0,
    Kill = 1,
    Vent = 2,
    Swoop = 4,
    Vest = 8,
}

/// <summary>
/// Implemented by any role whose kit can grow at runtime (Gooper, Kirby) rather than via a full role
/// swap. Both properties are plain mutable state, not modifiers - they're set directly inside an
/// already-deterministic RPC/event handler on every client, the same way <c>VultureRole.EatenBodies</c>
/// is incremented in <see cref="SuperSquadBodies.RpcVultureEat"/>.
/// </summary>
public interface IAbilityGrantHolder
{
    /// <summary>Gets or sets the non-kit ability primitives this role has unlocked.</summary>
    GrantableAbility UnlockedAbilities { get; set; }

    /// <summary>
    /// Gets the set of this addon's role types whose *real* buttons this role has borrowed. Any button
    /// extending <c>SuperSquadRoleButton&lt;TRole&gt;</c> shows for a holder whose set contains TRole -
    /// the ability IS the source role's ability, not a copy.
    /// </summary>
    HashSet<Type> GrantedKits { get; }

    /// <summary>
    /// Gets a value indicating whether this role can vent from its own base options alone, ignoring any
    /// granted <see cref="GrantableAbility.Vent"/> flag - <see cref="AbilityGrants.SyncVenting"/>
    /// combines the two.
    /// </summary>
    bool BaseCanVent { get; }
}

/// <summary>
/// One entry of transferable power: flag primitives, a borrowed button kit, or both.
/// </summary>
/// <param name="Flags">Non-kit ability primitives to OR in.</param>
/// <param name="Kit">A role type from this addon whose real buttons the recipient borrows, or null.</param>
public readonly record struct PortableGrant(GrantableAbility Flags, Type? Kit);

/// <summary>
/// The shared "grow a role's kit at runtime" machinery both Gooper and Kirby build on.
/// <see cref="ApplyPortableGrant"/> is the single transfer function: it maps any victim role - from
/// this addon, TOU-Mira, or vanilla - to what the recipient inherits, with no per-ability copies for
/// this addon's own roles.
/// </summary>
public static class AbilityGrants
{
    /// <summary>
    /// Sentinel pool index meaning "nothing left to draw", RPC-safe as a byte.
    /// </summary>
    public const byte NoPoolChoice = byte.MaxValue;

    /// <summary>
    /// Mafia roles are excluded from kit transfer: their buttons are entangled with team-membership
    /// machinery (promotion chain, shared labels/gates keyed on the actual role) that a borrower
    /// doesn't participate in. A digested mafia member still hands over generic flags (Kill/Vent).
    /// MafiaJanitor is NOT excluded - its clean ability is self-contained.
    /// </summary>
    private static readonly Type[] KitExcludedRoles =
        [typeof(Roles.Impostor.GodfatherRole), typeof(Roles.Impostor.MafiosoRole)];

    /// <summary>
    /// Abilities Gooper draws from at random (without replacement) starting with its 3rd goop.
    /// Kit entries grant the source role's real buttons; flag entries grant recreated primitives.
    /// Referenced by pool INDEX in <see cref="SuperSquadGooper.RpcGoop"/> so the draw serializes as a
    /// byte no matter what an entry contains.
    /// </summary>
    public static readonly PortableGrant[] Pool =
    [
        new(GrantableAbility.None, typeof(Roles.Impostor.SniperRole)),
        new(GrantableAbility.Swoop, null),
        new(GrantableAbility.None, typeof(Roles.Crewmate.DaddyHagridRole)),
    ];

    /// <summary>
    /// Checks whether <paramref name="role"/> either IS <paramref name="roleType"/> or has borrowed
    /// that role's button kit. This is the check every <c>SuperSquadRoleButton</c> gates
    /// <c>Enabled</c> on, and the check relaxed RPC validators use via
    /// <see cref="SenderIsOrHolds{TRole}"/>.
    /// </summary>
    /// <param name="role">The role to test (usually the local player's or an RPC sender's).</param>
    /// <param name="roleType">The role type whose kit is being exercised.</param>
    /// <returns>True if the role may use that kit's buttons.</returns>
    public static bool IsOrHolds(RoleBehaviour? role, Type roleType)
    {
        return roleType.IsInstanceOfType(role) ||
               (role is IAbilityGrantHolder holder && holder.GrantedKits.Contains(roleType));
    }

    /// <summary>
    /// RPC-validator form of <see cref="IsOrHolds"/>: accepts the sender if they hold
    /// <typeparamref name="TRole"/> natively or as a granted kit.
    /// </summary>
    /// <typeparam name="TRole">The role whose ability the RPC exercises.</typeparam>
    /// <param name="source">The RPC sender.</param>
    /// <returns>True if the sender may exercise that role's abilities.</returns>
    public static bool SenderIsOrHolds<TRole>(PlayerControl? source)
        where TRole : RoleBehaviour
    {
        return source != null && IsOrHolds(source.Data?.Role, typeof(TRole));
    }

    /// <summary>
    /// THE transfer function: grants <paramref name="recipient"/> everything portable from
    /// <paramref name="victimRole"/>. A victim from this addon hands over its whole real button kit
    /// (plus Vent/Kill primitives); a fellow grant-holder hands over everything it had accumulated
    /// (but not its own core identity ability - goop and swallow don't transfer); TOU-Mira/vanilla
    /// roles hand over the curated flag mapping below. Deterministic on every client as long as it
    /// runs inside a synced RPC/modifier handler.
    /// </summary>
    /// <param name="recipient">The role gaining abilities.</param>
    /// <param name="victimRole">The role being absorbed.</param>
    /// <param name="overwrite">
    /// True to clear all previously granted abilities first (Kirby's non-accumulate lobby option) -
    /// the new victim's grant replaces the old ones instead of stacking.
    /// </param>
    public static void ApplyPortableGrant(IAbilityGrantHolder recipient, RoleBehaviour? victimRole, bool overwrite = false)
    {
        if (overwrite)
        {
            recipient.UnlockedAbilities = GrantableAbility.None;
            recipient.GrantedKits.Clear();
        }

        if (victimRole != null)
        {
            if (victimRole is IAbilityGrantHolder victimHolder)
            {
                recipient.UnlockedAbilities |= victimHolder.UnlockedAbilities;
                recipient.GrantedKits.UnionWith(victimHolder.GrantedKits);
            }
            else
            {
                var victimType = victimRole.GetType();
                if (victimType.Assembly == typeof(AbilityGrants).Assembly && !KitExcludedRoles.Contains(victimType))
                {
                    recipient.GrantedKits.Add(victimType);
                }

                if (victimRole.CanVent)
                {
                    recipient.UnlockedAbilities |= GrantableAbility.Vent;
                }

                // Covers vanilla Impostors (CanUseKillButton) and modded roles configured to keep the
                // vanilla kill button. TOU-Mira roles with bespoke kill buttons (Sheriff etc.) grant
                // nothing here - their buttons can't be borrowed (see docs/architecture.md).
                if (victimRole.CanUseKillButton ||
                    victimRole is ICustomRole { Configuration.UseVanillaKillButton: true })
                {
                    recipient.UnlockedAbilities |= GrantableAbility.Kill;
                }

                if (victimRole is TownOfUs.Roles.Impostor.SwooperRole)
                {
                    recipient.UnlockedAbilities |= GrantableAbility.Swoop;
                }
            }
        }

        SyncVenting(recipient);
    }

    /// <summary>
    /// Applies one <see cref="Pool"/> entry by index (Gooper's 3rd+ goop reward). No-ops on
    /// <see cref="NoPoolChoice"/> or out-of-range.
    /// </summary>
    /// <param name="recipient">The role gaining the pool ability.</param>
    /// <param name="poolIndex">Index into <see cref="Pool"/> as drawn by <see cref="PickRandomPoolIndex"/>.</param>
    public static void ApplyPoolGrant(IAbilityGrantHolder recipient, byte poolIndex)
    {
        if (poolIndex >= Pool.Length)
        {
            return;
        }

        var grant = Pool[poolIndex];
        recipient.UnlockedAbilities |= grant.Flags;
        if (grant.Kit != null)
        {
            recipient.GrantedKits.Add(grant.Kit);
        }

        SyncVenting(recipient);
    }

    /// <summary>
    /// Picks the index of one <see cref="Pool"/> entry <paramref name="holder"/> doesn't hold yet,
    /// without replacement (confirmed design decision - repeats would waste a goop). Returns
    /// <see cref="NoPoolChoice"/> once the pool is exhausted.
    /// </summary>
    /// <remarks>
    /// Must be called on exactly one client (the acting Gooper, before sending the goop RPC) and the
    /// result passed through as an RPC argument so every client applies the identical draw - the same
    /// split <see cref="SuperSquadAmongUs.Events.ElusiveEvents"/> uses for its teleport destination.
    /// </remarks>
    /// <param name="holder">The role drawing from the pool.</param>
    /// <returns>An index into <see cref="Pool"/>, or <see cref="NoPoolChoice"/>.</returns>
    public static byte PickRandomPoolIndex(IAbilityGrantHolder holder)
    {
        var remaining = Enumerable.Range(0, Pool.Length)
            .Where(i => !Holds(holder, Pool[i]))
            .ToList();
        return remaining.Count == 0
            ? NoPoolChoice
            : (byte)remaining[UnityEngine.Random.Range(0, remaining.Count)];
    }

    /// <summary>
    /// Re-derives a grant-holder's vent capability (base option OR granted Vent flag) and pushes it
    /// into the places that don't re-read it live.
    /// </summary>
    /// <remarks>
    /// Two things must happen. (1) <c>Configuration.CanUseVent</c> already reads the Vent flag live (it
    /// drives <c>Vent.CanUse</c>), but the vanilla <c>RoleBehaviour.CanVent</c> bool is baked once at
    /// role setup by MiraAPI's <c>CustomRoleManager</c> - set it so every cached-bool vent path agrees.
    /// (2) The on-screen <c>ImpostorVentButton</c>'s visibility is only applied inside
    /// <c>HudManager.SetHudActive</c> (a MiraAPI postfix), which is NOT re-run per frame - so re-run it
    /// on the owner's own client so the vent button appears (or disappears, after an overwrite that
    /// dropped Vent) immediately.
    /// </remarks>
    /// <param name="holder">The grant-holding role to sync.</param>
    public static void SyncVenting(IAbilityGrantHolder holder)
    {
        if (holder is not RoleBehaviour role || role.Player == null)
        {
            return;
        }

        role.CanVent = holder.BaseCanVent || holder.UnlockedAbilities.HasFlag(GrantableAbility.Vent);

        if (role.Player.AmOwner && HudManager.InstanceExists)
        {
            HudManager.Instance.SetHudActive(PlayerControl.LocalPlayer, role, true);
        }
    }

    private static bool Holds(IAbilityGrantHolder holder, PortableGrant grant)
    {
        var holdsFlags = grant.Flags == GrantableAbility.None || holder.UnlockedAbilities.HasFlag(grant.Flags);
        var holdsKit = grant.Kit == null || holder.GrantedKits.Contains(grant.Kit);
        return holdsFlags && holdsKit;
    }
}
