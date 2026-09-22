using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using ModulithTemplate.SharedKernel.Outbox.Data;

using Underground.Outbox.Data;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Holds every feature context to the one shared outbox table, whatever it does in its own
/// <c>OnModelCreating</c>.
/// </summary>
/// <remarks>
/// A feature that omits <see cref="OutboxModelBuilderExtensions.MapSharedOutbox"/> compiles and
/// starts: the entity lands in that feature's own schema and its next migration creates a second
/// outbox table, which only surfaces as a failed claim at run time. Asserted against the model each
/// context builds, not against a call site, because a call that maps the wrong thing fails the same
/// way. <see cref="OutboxContext"/> is not a feature assembly and is excluded by discovery — it owns
/// the DDL.
/// </remarks>
public class OutboxMappingTests
{
    [Fact]
    public void Every_feature_context_stages_into_the_one_shared_table()
    {
        foreach (var (_, entity) in StagedMessages())
        {
            Assert.Equal(OutboxSchema.Name, entity.GetSchema(), StringComparer.Ordinal);
            Assert.Equal(OutboxSchema.TableName, entity.GetTableName(), StringComparer.Ordinal);
        }
    }

    [Fact]
    public void No_feature_migrates_the_shared_table()
    {
        foreach (var (context, entity) in StagedMessages())
        {
            Assert.True(
                entity.IsTableExcludedFromMigrations(),
                $"{context.Name} does not exclude {OutboxSchema.TableName} from its migrations; its first "
                    + "migration would create a second outbox table.");
        }
    }

    /// <summary>The outbox entity as each feature context maps it, with the context it came from.</summary>
    /// <remarks>
    /// The design-time model, not the runtime one: the migration exclusion above is a design-time
    /// concern and the read-optimized runtime model drops it.
    /// </remarks>
    private static (Type Context, IEntityType Entity)[] StagedMessages()
    {
        var contexts = SolutionAssemblies.FeatureAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsClass: true }
                && typeof(DbContext).IsAssignableFrom(type)
                && typeof(IOutboxDbContext).IsAssignableFrom(type))
            .ToArray();

        Assert.True(
            contexts.Length > 0,
            $"No feature {nameof(DbContext)} implementing {nameof(IOutboxDbContext)} was found; the outbox "
                + "mapping rules would pass vacuously.");

        return [.. contexts.Select(context => (context, Mapping(context)))];
    }

    private static IEntityType Mapping(Type context)
    {
        using var instance = Build(context);

        return instance.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(OutboxMessage))
            ?? throw new InvalidOperationException(
                $"{context.Name} implements {nameof(IOutboxDbContext)} but maps no {nameof(OutboxMessage)}.");
    }

    /// <summary>Constructs a context on options this test builds, not the ones the host registers.</summary>
    /// <remarks>
    /// Deliberate: the mapping lives in the context's own <c>OnModelCreating</c>, so it must hold
    /// however the context was registered. The provider only decides which relational annotations the
    /// model carries; nothing connects to the string.
    /// </remarks>
    private static DbContext Build(Type context)
    {
        var builder = (DbContextOptionsBuilder)Activator.CreateInstance(
            typeof(DbContextOptionsBuilder<>).MakeGenericType(context))!;
        builder.UseNpgsql("Host=localhost;Database=modulith_tests");

        var constructor = context.GetConstructor([typeof(DbContextOptions<>).MakeGenericType(context)])
            ?? throw new InvalidOperationException(
                $"{context.Name} has no public constructor taking DbContextOptions<{context.Name}>, so this rule "
                    + "cannot build its model. Scaffolded feature contexts declare one.");

        return (DbContext)constructor.Invoke([builder.Options]);
    }
}
