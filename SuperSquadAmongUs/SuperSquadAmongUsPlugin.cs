using System.Globalization;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using MiraAPI;
using MiraAPI.PluginLoading;
using Reactor;
using Reactor.Networking;
using Reactor.Networking.Attributes;
using Reactor.Utilities;
using TownOfUs;

namespace SuperSquadAmongUs;

[BepInAutoPlugin("dgrondona.supersquadamongus", "Super Squad Among Us")]
[BepInProcess("Among Us.exe")]
[BepInDependency(ReactorPlugin.Id)]
[BepInDependency(MiraApiPlugin.Id)]
[BepInDependency(TownOfUsPlugin.Id)]
[ReactorModFlags(ModFlags.RequireOnAllClients)]
public partial class SuperSquadAmongUsPlugin : BasePlugin, IMiraPlugin
{
    /// <summary>
    ///     Gets the specified Culture for string manipulations.
    /// </summary>
    public static CultureInfo Culture => TownOfUs.TownOfUsPlugin.Culture;

    /// <inheritdoc />
    public string OptionsTitleText => "Super Squad Among Us";

    /// <summary>
    ///     Determines if the current build is a dev build or not. This will change certain visuals as well as always grab news locally to be up to date.
    /// </summary>
    /// <remarks>
    ///     Derived from the version rather than hardcoded, mirroring TOU-Mira's own
    ///     <c>TownOfUsPlugin</c> and MiraAPI's <c>MiraApiPlugin</c>, so the build channel is set purely
    ///     by <c>VersionSuffix</c> at build time - see the build notes in CLAUDE.md.
    /// </remarks>
    public static bool IsDevBuild => IsBetaBuild || IsWipBuild;

    /// <summary>
    ///     Determines if the current build is a beta build. Beta builds count as dev builds, but are
    ///     the channel that should keep debug-only affordances switched off.
    /// </summary>
    public static bool IsBetaBuild => Version.Contains("beta", StringComparison.OrdinalIgnoreCase) ||
                                      Version.Contains("prerelease", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Determines if the current build is a work-in-progress build - a local <c>dev</c> build or a
    ///     CI build.
    /// </summary>
    public static bool IsWipBuild => Version.Contains("dev", StringComparison.OrdinalIgnoreCase) ||
                                     Version.Contains("ci", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public ConfigFile GetConfigFile()
    {
        return Config;
    }

    public Harmony Harmony { get; } = new(Id);

    public override void Load()
    {
        ReactorCredits.Register("Super Squad Among Us", Version, IsDevBuild, ReactorCredits.AlwaysShow);
        Modules.SuperSquadLocale.Register();

        Harmony.PatchAll();
    }
}
