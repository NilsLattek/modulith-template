using Microsoft.EntityFrameworkCore;

using ModulithTemplate.SharedKernel.Application.Events;

namespace ModulithTemplate.SharedKernel.Outbox.Events;

/// <summary>The inbox route of one feature context.</summary>
/// <typeparam name="TContext">The feature's context.</typeparam>
public sealed class InboxRoute<TContext> : IInboxRoute
    where TContext : DbContext
{
    /// <inheritdoc />
    public string Consumer => typeof(TContext).Name;

    /// <inheritdoc />
    public object Wrap<TEvent>(TEvent integrationEvent)
        where TEvent : IIntegrationEvent =>
        new InboxEnvelope<TContext, TEvent>(integrationEvent);
}
