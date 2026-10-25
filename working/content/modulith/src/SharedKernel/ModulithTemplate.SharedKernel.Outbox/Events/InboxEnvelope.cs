using System.Diagnostics.CodeAnalysis;
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
        new(RowId(), createdAt, this, $"{typeof(TConsumerContext).Name}:{Event.GroupKey}");

    /// <summary>The row's identity: a UUIDv5 of this envelope's type name, in the event's namespace.</summary>
    /// <remarks>
    /// Deterministic, so a redelivered event collides with its own row instead of adding a second, and
    /// distinct per Consumer. The library orders a Group by transaction and append order, never by this.
    /// </remarks>
    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "RFC 9562 fixes SHA-1 for UUIDv5; nothing here is secret.")]
    [SuppressMessage("Security", "S4790:Using weak hashing algorithms is security-sensitive", Justification = "RFC 9562 fixes SHA-1 for UUIDv5; nothing here is secret.")]
    private Guid RowId()
    {
        var name = Encoding.UTF8.GetBytes(GetType().FullName!);
        var input = new byte[16 + name.Length];
        Event.EventId.TryWriteBytes(input, bigEndian: true, out _);
        name.CopyTo(input, 16);

        Span<byte> hash = stackalloc byte[SHA1.HashSizeInBytes];
        SHA1.HashData(input, hash);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16], bigEndian: true);
    }
}
