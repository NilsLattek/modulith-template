using FluentResults;

using Mediator;

using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Contracts.Events;
using ModulithTemplate.SharedKernel.Infrastructure;
using ModulithTemplate.SharedKernel.Infrastructure.Events;

using Underground.Outbox.Configuration;
using Underground.Outbox.Data;

namespace ModulithTemplate.TemplateTests;

/// <summary>
/// A second Consumer of Orders' event, wired as a feature would be, that refuses one entity name.
/// </summary>
/// <remarks>
/// Lives only here, so the shipped solution keeps one Consumer. Its handler writes before refusing, so a
/// test can tell a refusal that kept its writes from one that rolled them back.
/// </remarks>
public static class Auditing
{
    /// <summary>The entity name this Consumer refuses.</summary>
    public const string Refused = "refused";

    /// <summary>Registers the Consumer, as a feature's <c>Configure*Feature</c> would.</summary>
    /// <param name="builder">The solution's builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static WebApplicationBuilder ConfigureAuditing(this WebApplicationBuilder builder)
    {
        builder.Services.AddModuleDbContext<AuditContext>(builder.Configuration, schema: "audit");
        builder.Services.AddModulithTemplateTemplateTestsMessageHandlers();
        return builder;
    }

    /// <summary>The audit entries this Consumer has recorded.</summary>
    /// <param name="solution">The composed solution.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entries.</returns>
    internal static async Task<IReadOnlyList<AuditEntry>> AuditEntriesAsync(
        this SolutionUnderTest solution, CancellationToken cancellationToken)
    {
        using var scope = solution.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AuditContext>()
            .Set<AuditEntry>().AsNoTracking().ToListAsync(cancellationToken);
    }

    /// <summary>A record that an entity was announced.</summary>
    public sealed class AuditEntry
    {
        public Guid Id { get; private set; } = Guid.CreateVersion7();

        public string Name { get; private set; } = "";

        public static AuditEntry For(string name) => new() { Name = name };
    }

    /// <summary>The Consumer's context, shaped like a feature's.</summary>
    /// <param name="options">The context options.</param>
    public sealed class AuditContext(DbContextOptions<AuditContext> options)
        : DbContext(options), IOutboxDbContext, IInboxDbContext
    {
        public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

        public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema("audit");
            modelBuilder.Entity<AuditEntry>();
            modelBuilder.ApplySharedModel();
        }
    }

    /// <summary>Records an audit entry, refusing <see cref="Refused"/> after writing it.</summary>
    /// <param name="Name">The announced entity's name.</param>
    public sealed record RecordAuditCommand(string Name) : ICommand<Result>;

    /// <summary>Handles <see cref="RecordAuditCommand"/>.</summary>
    /// <param name="context">The Consumer's context.</param>
    public sealed class RecordAuditCommandHandler(AuditContext context) : ICommandHandler<RecordAuditCommand, Result>
    {
        /// <inheritdoc />
        public async ValueTask<Result> Handle(RecordAuditCommand command, CancellationToken cancellationToken)
        {
            context.Add(AuditEntry.For(command.Name));
            await context.SaveChangesAsync(cancellationToken);
            return string.Equals(command.Name, Refused, StringComparison.Ordinal)
                ? Result.Fail("refused")
                : Result.Ok();
        }
    }

    /// <summary>The Consumer's inbox handler.</summary>
    /// <param name="services">The provider of the processing scope.</param>
    public sealed class SomeEntityAddedInboxHandler(IServiceProvider services)
        : IntegrationEventInboxTransport<SomeEntityAddedIntegrationEvent, AuditContext>(services)
    {
        /// <inheritdoc />
        protected override ICommand<Result> ToCommand(SomeEntityAddedIntegrationEvent integrationEvent) =>
            new RecordAuditCommand(integrationEvent.Name);
    }
}
