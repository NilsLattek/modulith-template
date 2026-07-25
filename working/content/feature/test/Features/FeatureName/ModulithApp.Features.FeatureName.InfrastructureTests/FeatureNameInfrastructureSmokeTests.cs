using Microsoft.EntityFrameworkCore;

using ModulithApp.Features.FeatureName.Infrastructure.Data;

namespace ModulithApp.Features.FeatureName.InfrastructureTests;

/// <summary>Smoke tests for the FeatureName infrastructure layer.</summary>
public class FeatureNameInfrastructureSmokeTests
{
    /// <summary>
    /// Every feature owns its schema, set via <c>HasDefaultSchema</c> in <c>OnModelCreating</c>.
    /// Building the model is enough to assert it — Npgsql does not open a connection to do so,
    /// which keeps this a unit test with no database dependency.
    /// </summary>
    [Fact]
    public void FeatureNameContext_model_defaults_to_the_feature_schema()
    {
        // Arrange — a syntactically valid connection string is required; nothing connects to it.
        var options = new DbContextOptionsBuilder<FeatureNameContext>()
            .UseNpgsql("Host=localhost;Database=modulith_tests")
            .Options;

        // Act
        using var context = new FeatureNameContext(options);
        var defaultSchema = context.Model.GetDefaultSchema();

        // Assert
        Assert.Equal("featureschema", defaultSchema);
    }
}
