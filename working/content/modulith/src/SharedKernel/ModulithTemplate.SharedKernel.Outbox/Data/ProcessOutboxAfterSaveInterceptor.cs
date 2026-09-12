using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using Underground.Outbox;
using Underground.Outbox.Data;

namespace ModulithTemplate.SharedKernel.Outbox.Data;

/// <summary>
/// Asks the outbox worker to run once a save has written staged messages.
/// </summary>
/// <remarks>
/// Staging outside an explicit transaction triggers no push-based processing, so without this the
/// event waits for the next poll. The library's own interceptor pushes on transaction commit and is
/// attached alongside; whichever fires first, an extra nudge only costs a wakeup.
/// <para>
/// Resolved from the provider on demand rather than injected, because the outbox services are
/// registered by the host and a feature context can be built without them — in a test, for one.
/// </para>
/// </remarks>
/// <param name="services">The scope the context was resolved from.</param>
public sealed class ProcessOutboxAfterSaveInterceptor(IServiceProvider services) : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        // Read after the save, not before: the translating handler stages its row during dispatch,
        // which another interceptor performs, so asking earlier would depend on interceptor order.
        if (eventData.Context?.ChangeTracker.Entries<OutboxMessage>().Any() == true)
        {
            services.GetService<IOutbox>()?.ProcessMessages();
        }

        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
