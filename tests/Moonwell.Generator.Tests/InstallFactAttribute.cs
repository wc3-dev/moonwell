using Moonwell.GameData;

namespace Moonwell.Generator.Tests;

/// <summary>A fact that reads the Warcraft III install on this computer (MOONWELL_WAR3, else the usual folder): skipped when there is none.</summary>
public sealed class InstallFactAttribute : FactAttribute
{
    /// <summary>Skips the fact, naming the place looked in, when no install is found.</summary>
    public InstallFactAttribute()
    {
        if (GameInstall.Find() == null)
        {
            Skip = $"No Warcraft III install in {string.Join(", ", GameInstall.Places())}; set {GameInstall.Variable} to run it";
        }
    }
}
