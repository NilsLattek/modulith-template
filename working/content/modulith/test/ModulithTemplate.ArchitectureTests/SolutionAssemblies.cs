using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

using Assembly = System.Reflection.Assembly;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Shared discovery and loading of the compiled feature-layer assemblies used by the
/// architecture tests. Assemblies are matched by file name containing the <c>.Features.</c>
/// segment, excluding any whose name ends in <c>Tests</c>, which corresponds to the
/// per-feature layer projects while excluding their test projects.
/// </summary>
internal static class SolutionAssemblies
{
    private const string FeaturesKeyword = ".Features.";

    private static readonly Lazy<Assembly[]> LazyFeatureAssemblies = new(LoadFeatureAssemblies);
    private static readonly Lazy<Architecture> LazyArchitecture = new(BuildArchitecture);

    /// <summary>The compiled feature-layer assemblies discovered under the solution directory.</summary>
    public static Assembly[] FeatureAssemblies => LazyFeatureAssemblies.Value;

    /// <summary>The ArchUnitNET architecture built from <see cref="FeatureAssemblies"/>.</summary>
    public static Architecture Architecture => LazyArchitecture.Value;

    /// <summary>Walks up from the current working directory to the folder containing the .slnx.</summary>
    public static string GetSolutionDirectory()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && directory.GetFiles("*.slnx").Length == 0)
        {
            directory = directory.Parent;
        }
        return directory!.FullName;
    }

    private static Assembly[] LoadFeatureAssemblies()
    {
        try
        {
            return Directory.GetFiles(GetSolutionDirectory(), "*.dll", SearchOption.AllDirectories)
                // Match the assembly's own file name, not the full path: a test project
                // directory such as ModulithTemplate.Features.Orders.WebTests would otherwise
                // pull its entire output (xunit, bUnit, AngleSharp, NSubstitute, ...) into the
                // architecture. Assemblies ending in "Tests" are excluded for the same reason.
                .Where(f => Path.GetFileName(f).Contains(FeaturesKeyword, StringComparison.Ordinal)
                    && !Path.GetFileNameWithoutExtension(f).EndsWith("Tests", StringComparison.Ordinal))
                .DistinctBy(Path.GetFileName)
                .Select(Assembly.LoadFile)
                .ToArray();
        }
        catch (BadImageFormatException)
        {
            return [];
        }
    }

    private static Architecture BuildArchitecture() =>
        new ArchLoader().LoadAssemblies(FeatureAssemblies).Build();
}
