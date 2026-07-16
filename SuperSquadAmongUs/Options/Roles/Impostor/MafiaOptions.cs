using MiraAPI.GameOptions;
using MiraAPI.GameOptions.Attributes;
using MiraAPI.Utilities;
using TownOfUs.Modules.Localization;

namespace SuperSquadAmongUs.Options.Roles.Impostor;

/// <summary>
/// Shared options for the Mafia trio (Godfather, Mafioso, MafiaJanitor). Single spawn roll for the whole trio, TOR-faithful (see docs/porting/tor-mafia.md).
/// </summary>
public sealed class MafiaOptions : AbstractOptionGroup
{
    public override string GroupName => TouLocale.Get("SuperSquadRoleMafia", "Mafia");

    [ModdedNumberOption("SuperSquadOptionMafiaSpawnChance", 0, 100, 10, MiraNumberSuffixes.Percent)]
    public float SpawnChance { get; set; } = 50f;

    [ModdedNumberOption("SuperSquadOptionMafiaJanitorCleanCooldown", 10f, 60f, 2.5f, MiraNumberSuffixes.Seconds)]
    public float JanitorCleanCooldown { get; set; } = 30f;
}
