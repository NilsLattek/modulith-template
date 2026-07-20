using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Slices;
using ArchUnitNET.xUnitV3;

namespace ModulithTemplate.ArchitectureTests;

public class FeatureModuleTests
{
    [Fact]
    public void ModulesCannotDependOnEachOther()
    {
        const string modulesKeyword = ".Features.";
        const string integrationEventsKeyword = ".IntegrationEvents";

        // Guard: fail loudly if no feature assemblies were discovered, so the cross-feature
        // rule cannot pass vacuously on an empty architecture.
        Assert.NotEmpty(SolutionAssemblies.FeatureAssemblies);

        Architecture architecture = SolutionAssemblies.Architecture;

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
