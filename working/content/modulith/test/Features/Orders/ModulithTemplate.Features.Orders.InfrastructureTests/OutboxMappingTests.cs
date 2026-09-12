using Mediator;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ModulithTemplate.Features.Orders.Infrastructure.Data;
using ModulithTemplate.SharedKernel.Infrastructure;
using ModulithTemplate.SharedKernel.Outbox.Data;

using NSubstitute;

using Underground.Outbox.Data;

namespace ModulithTemplate.Features.Orders.InfrastructureTests;

/// <summary>
/// Pins what <c>AddModuleDbContext</c> does to a feature context's outbox mapping.
/// </summary>
/// <remarks>
/// Asserted through the shared registration rather than the context directly, because that is where
/// the mapping is applied: a feature declares only the <c>DbSet</c>. Getting this wrong is invisible
/// until a migration is generated — the entity would land in the feature's own schema and the
/// feature would try to create a second outbox table of its own.
/// </remarks>
public class OutboxMappingTests
{
    private static IEntityType StagedMessages()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                // Syntactically valid; nothing connects to it.
                ["ConnectionStrings:PostgresConnection"] = "Host=localhost;Database=modulith_tests",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.ClearProviders());
        services.AddSingleton(Substitute.For<IPublisher>());
        services.AddModuleDbContext<OrdersContext>(configuration, schema: "orders");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrdersContext>();

        // The design-time model, not the runtime one: the exclusion below is a migration concern, and
        // the read-optimized runtime model drops it.
        return context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(OutboxMessage))!;
    }

    [Fact]
    public void A_feature_context_stages_into_the_one_shared_table()
    {
        // Act
        var entity = StagedMessages();

        // Assert
        Assert.Equal(OutboxSchema.Name, entity.GetSchema(), StringComparer.Ordinal);
        Assert.Equal(OutboxSchema.TableName, entity.GetTableName(), StringComparer.Ordinal);
    }

    [Fact]
    public void A_feature_does_not_migrate_the_shared_table()
    {
        // Act
        var entity = StagedMessages();

        // Assert
        // Only OutboxContext owns the DDL (ADR 0001); without this each feature's first migration
        // would try to create the table again.
        Assert.True(entity.IsTableExcludedFromMigrations());
    }
}
