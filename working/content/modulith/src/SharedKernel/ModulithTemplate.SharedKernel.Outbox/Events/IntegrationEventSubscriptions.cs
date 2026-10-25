using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

using ModulithTemplate.SharedKernel.Application.Events;

using Underground.Outbox.Data;
using Underground.Outbox.Domain.Dispatchers;

namespace ModulithTemplate.SharedKernel.Outbox.Events;

/// <summary>
/// Who receives an Integration Event: the Consumers with an inbox handler for it, and an outbox
/// handler forwarding it outside the process.
/// </summary>
/// <remarks>
/// Read from the handlers the outbox source generator registered, once, so a Publisher never names a
/// Consumer. A singleton, registered by <c>AddModuleDbContext</c>.
/// </remarks>
/// <param name="routes">Every feature context's inbox route.</param>
/// <param name="inboxHandlers">The registered inbox handlers.</param>
/// <param name="outboxHandlers">The registered outbox handlers.</param>
public sealed class IntegrationEventSubscriptions(
    IEnumerable<IInboxRoute> routes,
    IEnumerable<HandlerEntry<InboxMessage>> inboxHandlers,
    IEnumerable<HandlerEntry<OutboxMessage>> outboxHandlers)
{
    private readonly IInboxRoute[] _routes = [.. routes];
    private readonly HashSet<string> _inboxTypes = new(inboxHandlers.Select(entry => entry.MessageTypeName), StringComparer.Ordinal);
    private readonly HashSet<Type> _outboxTypes = [.. outboxHandlers.Select(entry => entry.MessageType)];

    /// <summary>One inbox row for each Consumer with a handler for the event.</summary>
    /// <typeparam name="TEvent">The event's type.</typeparam>
    /// <param name="integrationEvent">The event.</param>
    /// <param name="createdAt">When it was published, in UTC.</param>
    /// <returns>The rows, none when no feature consumes it.</returns>
    public IReadOnlyList<InboxMessage> InboxRows<TEvent>(TEvent integrationEvent, DateTime createdAt)
        where TEvent : IIntegrationEvent =>
        [
            .. _routes
                .Select(route => (Route: route, Envelope: route.Wrap(integrationEvent)))
                .Select(target => (target.Route, target.Envelope, Type: target.Envelope.GetType().FullName!))
                .Where(target => _inboxTypes.Contains(target.Type))
                .Select(target => new InboxMessage(
                    RowId(integrationEvent.EventId, target.Type),
                    createdAt,
                    target.Envelope,
                    $"{target.Route.Consumer}:{integrationEvent.GroupKey}")),
        ];

    /// <summary>Whether an outbox handler forwards the event outside the process.</summary>
    /// <typeparam name="TEvent">The event's type.</typeparam>
    /// <returns><see langword="true"/> when one is registered.</returns>
    public bool IsForwarded<TEvent>()
        where TEvent : IIntegrationEvent =>
        _outboxTypes.Contains(typeof(TEvent));

    /// <summary>The row's identity: a UUIDv5 named by the envelope type, in the event's namespace.</summary>
    /// <remarks>
    /// Deterministic, so a replayed save stages the same ids, and distinct per Consumer, as the inbox's
    /// unique index needs. The library orders a Group by transaction and append order, never by this.
    /// </remarks>
    /// <param name="eventId">The event's identity, used as the UUID namespace.</param>
    /// <param name="envelopeType">The envelope's type name.</param>
    /// <returns>The row's EventId.</returns>
    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "RFC 9562 fixes SHA-1 for UUIDv5; nothing here is secret.")]
    [SuppressMessage("Security", "S4790:Using weak hashing algorithms is security-sensitive", Justification = "RFC 9562 fixes SHA-1 for UUIDv5; nothing here is secret.")]
    private static Guid RowId(Guid eventId, string envelopeType)
    {
        var name = Encoding.UTF8.GetBytes(envelopeType);
        var input = new byte[16 + name.Length];
        eventId.TryWriteBytes(input, bigEndian: true, out _);
        name.CopyTo(input, 16);

        Span<byte> hash = stackalloc byte[SHA1.HashSizeInBytes];
        SHA1.HashData(input, hash);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16], bigEndian: true);
    }
}
