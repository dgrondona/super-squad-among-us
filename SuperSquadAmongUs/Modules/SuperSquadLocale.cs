using MiraAPI.Translation;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// Registers this addon's embedded <c>Resources/Locale/*.xml</c> files with MiraAPI's translation
/// system.
/// </summary>
public static class SuperSquadLocale
{
    /// <summary>
    /// Registers our locale XMLs. Call once from <see cref="SuperSquadAmongUsPlugin.Load"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="MiraLocaleManager.Register(string, string)"/> resolves embedded resources off
    /// <c>Assembly.GetCallingAssembly()</c>, so this must stay inside this assembly. The second
    /// argument is the resource-embed root namespace - MiraAPI looks for
    /// <c>SuperSquadAmongUs.Resources.Locale.&lt;lang&gt;.xml</c>.
    /// </remarks>
    public static void Register()
    {
        MiraLocaleManager.Register(SuperSquadAmongUsPlugin.Id, "SuperSquadAmongUs");
    }
}
