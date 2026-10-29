using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;

using ModulithTemplate.SharedKernel.Application.Events;

using Underground.Outbox.Data;

namespace ModulithTemplate.SharedKernel.Outbox.Events;

/// <summary>An Integration Event addressed to one Consumer: the payload of one <c>shared.inbox</c> row.</summary>
/// <remarks>
/// The library dispatches a stored type name to exactly one handler, so the Consumer's context is a type
/// argument only to make that name distinct per Consumer. The name embeds each type argument's assembly
/// version, which is why <c>Directory.Build.props</c> pins <c>AssemblyVersion</c>.
/// </remarks>
/// <typeparam name="TConsumerContext">The consuming feature's context.</typeparam>
/// <typeparam name="TEvent">The event delivered.</typeparam>
/// <param name="Event">The event delivered.</param>
public sealed record InboxEnvelope<TConsumerContext, TEvent>(TEvent Event)
    where TConsumerContext : DbContext
    where TEvent : IIntegrationEvent
{
    /// <summary>This envelope as its Consumer's inbox row, grouped per Consumer and aggregate.</summary>
    /// <param name="createdAt">When the row is staged, in UTC.</param>
    /// <returns>The row to stage.</returns>
    public InboxMessage ToInboxMessage(DateTime createdAt) =>
        new(DeterministicRowId(), createdAt, this, $"{typeof(TConsumerContext).Name}:{Event.GroupKey}");

    /// <summary>The row's identity: <c>{envelope type}_{EventId}</c>, hashed to the Guid the library stores.</summary>
    /// <remarks>
    /// Deterministic, so a redelivered event collides with its own row instead of adding a second, and
    /// distinct per Consumer. The library orders a Group by transaction and append order, never by this.
    /// </remarks>
    private Guid DeterministicRowId() =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{GetType().FullName}_{Event.EventId}")).AsSpan(0, 16));
}
