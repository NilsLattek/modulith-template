using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using ModulithTemplate.SharedKernel.Outbox.Data;

using Underground.Outbox.Data;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Holds every feature context to the one shared outbox table and the one shared inbox table, whatever
/// it does in its own <c>OnModelCreating</c>.
/// </summary>
/// <remarks>
/// A feature that omits <c>ApplySharedModel()</c>, which maps both, compiles and starts: the entity
/// lands in that feature's own schema and its next migration creates a second table, which only
/// surfaces as a failed claim or publish at run time. Asserted against the model each context builds,
/// not against a call site, because a call that maps the wrong thing fails the same way.
/// <see cref="OutboxContext"/> and <see cref="InboxContext"/> are not feature assemblies and are excluded
/// by discovery — they own the DDL.
/// </remarks>
public class SharedTableMappingTests
{
    /// <summary>Each shared table, by the message entity and the context interface that stages into it.</summary>
    public static TheoryData<Type, Type, string> SharedTables => new()
    {
        { typeof(OutboxMessage), typeof(IOutboxDbContext), OutboxSchema.TableName },
        { typeof(InboxMessage), typeof(IInboxDbContext), OutboxSchema.InboxTableName },
    };

    [Theory]
    [MemberData(nameof(SharedTables))]
    public void Every_feature_context_stages_into_the_one_shared_table(Type message, Type contract, string table)
    {
        foreach (var (_, entity) in StagedMessages(message, contract))
        {
            Assert.Equal(OutboxSchema.Name, entity.GetSchema(), StringComparer.Ordinal);
            Assert.Equal(table, entity.GetTableName(), StringComparer.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(SharedTables))]
    public void No_feature_migrates_the_shared_table(Type message, Type contract, string table)
    {
        foreach (var (context, entity) in StagedMessages(message, contract))
        {
            Assert.True(
                entity.IsTableExcludedFromMigrations(),
                $"{context.Name} does not exclude {table} from its migrations; its first migration would "
                    + $"create a second {table} table.");
        }
    }

    [Fact]
    public void Every_feature_context_can_stage_into_the_inbox()
    {
        // Without it a feature can neither publish to a Consumer nor be enlisted as one.
        var missing = FeatureContexts()
            .Where(context => !typeof(IInboxDbContext).IsAssignableFrom(context))
            .Select(context => context.Name)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"{string.Join(", ", missing)} do not implement {nameof(IInboxDbContext)}.");
    }

    /// <summary>A shared message entity as each feature context maps it, with the context it came from.</summary>
    /// <remarks>
    /// The design-time model, not the runtime one: the migration exclusion above is a design-time
    /// concern and the read-optimized runtime model drops it.
    /// </remarks>
    private static (Type Context, IEntityType Entity)[] StagedMessages(Type message, Type contract)
    {
        var contexts = FeatureContexts().Where(contract.IsAssignableFrom).ToArray();

        Assert.True(
            contexts.Length > 0,
            $"No feature {nameof(DbContext)} implementing {contract.Name} was found; the mapping rules would "
                + "pass vacuously.");

        return [.. contexts.Select(context => (context, Mapping(context, message, contract)))];
    }

    private static Type[] FeatureContexts() =>
        [.. SolutionAssemblies.FeatureAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsClass: true } && typeof(DbContext).IsAssignableFrom(type))];

    private static IEntityType Mapping(Type context, Type message, Type contract)
    {
        using var instance = Build(context);

        return instance.GetService<IDesignTimeModel>().Model.FindEntityType(message)
            ?? throw new InvalidOperationException(
                $"{context.Name} implements {contract.Name} but maps no {message.Name}.");
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
