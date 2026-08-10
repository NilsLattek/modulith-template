using ModulithTemplate.Application.Common.Events;

namespace ModulithTemplate.Web.Events;

/// <summary>
/// Logs every integration event that is published, whatever its type.
/// </summary>
/// <typeparam name="TEvent">Any integration event.</typeparam>
/// <remarks>
/// An open generic handler constrained to <see cref="IIntegrationEvent"/>, which the mediator
/// registers as a constrained open type so it applies to every current and future event without
/// being named anywhere.
/// <para>
/// It also does necessary work beyond observability. The mediator's source generator raises
/// <c>MSG0005</c> for a message type with no registered handler, and CI builds with
/// <c>-warnaserror</c>: without a catch-all, publishing an event that no feature has subscribed to
/// yet would <b>break the build</b>. That is the normal state of a freshly scaffolded feature, and
/// of any event whose only consumer was just deleted. Narrowing the diagnostic away with
/// <c>NoWarn</c> was the alternative, but it would also hide a genuinely unhandled command or query,
/// which is what MSG0005 is actually worth having for.
/// </para>
/// </remarks>
/// <param name="logger">Logger for this handler's closed generic type.</param>
public sealed class IntegrationEventLogHandler<TEvent>(ILogger<IntegrationEventLogHandler<TEvent>> logger)
    : IIntegrationEventHandler<TEvent>
    where TEvent : IIntegrationEvent
{
    /// <inheritdoc />
    public ValueTask Handle(TEvent notification, CancellationToken cancellationToken)
    {
        EventLog.IntegrationEventPublished(logger, typeof(TEvent).Name);
        return ValueTask.CompletedTask;
    }
}
