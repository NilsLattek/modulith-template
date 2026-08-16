using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

using Assembly = System.Reflection.Assembly;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Shared discovery of the feature-layer assemblies used by the architecture tests: everything in
/// this project's output whose simple name contains <c>.Features.</c> and does not end in
/// <c>Tests</c>.
/// </summary>
/// <remarks>
/// Reads the output directory, not the solution tree: the csproj references every
/// <c>src/Features/**</c> project, so each layer lands here built in the configuration under test.
/// A tree walk instead finds several copies per assembly (bin and obj, every configuration ever
/// built), and a leftover <c>bin/Release</c> would silently validate stale bits.
/// </remarks>
internal static class SolutionAssemblies
{
    private const string FeaturesKeyword = ".Features.";

    private static readonly Lazy<Assembly[]> LazyFeatureAssemblies = new(LoadFeatureAssemblies);
    private static readonly Lazy<Architecture> LazyArchitecture = new(BuildArchitecture);

    /// <summary>The compiled feature-layer assemblies discovered in this project's output directory.</summary>
    public static Assembly[] FeatureAssemblies => LazyFeatureAssemblies.Value;

    /// <summary>The ArchUnitNET architecture built from <see cref="FeatureAssemblies"/>.</summary>
    public static Architecture Architecture => LazyArchitecture.Value;

    /// <summary>Builds an assembly-name regex matching a feature layer by its suffix expression.</summary>
    /// <remarks>
    /// ArchUnitNET matches the fully-qualified name (<c>...Orders.Domain, Version=1.0.0.0, ...</c>),
    /// so the suffix anchors at the simple-name boundary — end of string or the version comma.
    /// </remarks>
    /// <param name="suffixExpression">A layer suffix, or a regex alternation of several.</param>
    /// <returns>The assembly-name pattern.</returns>
    public static string FeatureLayerPattern(string suffixExpression) =>
        @".*\.Features\..*\." + suffixExpression + @"(,.*)?$";

    /// <summary>Namespace of the tracker type a code-coverage run injects into every assembly.</summary>
    /// <remarks>
    /// <c>dotnet test --coverage</c> rewrites the assemblies in place, adding one identically-named
    /// tracker type to each. ArchUnitNET resolves those by name across the whole architecture, so
    /// the copies appear to depend on each other and every layering rule fails. Excluding the
    /// namespace from a rule's source set is enough.
    /// </remarks>
    public const string InstrumentationNamespacePattern = @"^Microsoft\.CodeCoverage\..*";

    /// <summary>Combines one or more layer suffixes into a regex alternation.</summary>
    /// <param name="suffixes">The suffixes to combine.</param>
    /// <returns>A single suffix expression.</returns>
    public static string Alternation(params string[] suffixes) =>
        suffixes.Length == 1 ? suffixes[0] : "(" + string.Join("|", suffixes) + ")";

    /// <summary>Whether any discovered feature assembly is the given layer.</summary>
    /// <param name="layerSuffix">The layer's assembly-name suffix, e.g. <c>Contracts</c>.</param>
    /// <returns><see langword="true"/> if at least one such assembly was loaded.</returns>
    public static bool HasFeatureLayer(string layerSuffix) =>
        FeatureAssemblies.Any(assembly =>
        {
            var name = assembly.GetName().Name!;
            return name.Contains(FeaturesKeyword, StringComparison.Ordinal)
                && name.EndsWith("." + layerSuffix, StringComparison.Ordinal);
        });

    private static Assembly[] LoadFeatureAssemblies()
    {
        try
        {
            return Directory.GetFiles(AppContext.BaseDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                // Simple name, never the file name: "Microsoft.Extensions.Features.dll" contains
                // ".Features." only because of the dot before the extension.
                .Where(path =>
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    return name.Contains(FeaturesKeyword, StringComparison.Ordinal)
                        && !name.EndsWith("Tests", StringComparison.Ordinal);
                })
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
