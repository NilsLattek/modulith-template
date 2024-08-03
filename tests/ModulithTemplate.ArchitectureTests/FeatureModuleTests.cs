using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Slices;
using ArchUnitNET.xUnit;

using Assembly = System.Reflection.Assembly;

namespace ModulithTemplate.ArchitectureTests;

public class FeatureModuleTests
{
    private static String GetSolutionDirectory()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && directory.GetFiles("*.sln").Length == 0)
        {
            directory = directory.Parent;
        }
        return directory!.FullName;
    }

    [Fact]
    public void ModulesCannotDependOnEachOther()
    {
        const string modulesKeyword = ".Features.";
        const string integrationEventsKeyword = ".IntegrationEvents";

        // load assemblies from DLL
        List<Assembly> assemblies = [];

        try
        {
            assemblies = Directory.GetFiles(GetSolutionDirectory(), "*.dll", SearchOption.AllDirectories)
                        .Where(f => f.Contains(modulesKeyword))
                        .DistinctBy(Path.GetFileName)
                        .Select(Assembly.LoadFile)
                        .ToList();
        }
        catch (System.BadImageFormatException)
        {
            // ignore
        }

        Architecture architecture = new ArchLoader()
            .LoadAssemblies(assemblies.ToArray())
            .Build();

        var moduleSlice = new SliceAssignment(t =>
        {
            var fullName = t.Namespace.FullName;
            var featureKeywordIdx = fullName.IndexOf(modulesKeyword, StringComparison.Ordinal);

            if (featureKeywordIdx < 0)
            {
                return SliceIdentifier.Ignore();
            }

            var withoutFeaturePrefix = fullName.Substring(featureKeywordIdx + modulesKeyword.Length);

            var nextDotIdx = withoutFeaturePrefix.IndexOf('.');
            var featureName = nextDotIdx < 0 ? withoutFeaturePrefix : withoutFeaturePrefix.Substring(0, nextDotIdx);

            if (withoutFeaturePrefix.StartsWith(featureName + integrationEventsKeyword, StringComparison.Ordinal))
            {
                return SliceIdentifier.Ignore();
            }

            return SliceIdentifier.Of(featureName);
        }, "module slice");

        var ruleCreator = new SliceRuleCreator();
        ruleCreator.SetSliceAssignment(moduleSlice);
        IArchRule noCrossFeatureReference = new GivenSlices(ruleCreator).Should().NotDependOnEachOther();

        noCrossFeatureReference.Check(architecture);
    }
}
