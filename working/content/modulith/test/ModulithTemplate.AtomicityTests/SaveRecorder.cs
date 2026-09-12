using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ModulithTemplate.AtomicityTests;

/// <summary>
/// Records what each save on a context was about to write.
/// </summary>
/// <remarks>
/// Attached after the registered interceptors, so a save is read once domain event dispatch has run
/// and whatever its handlers staged is in the change tracker. That is what lets a test say
/// <i>one</i> save carried both rows rather than only that both rows exist.
/// <para>
/// Synchronous saves are recorded too, though they dispatch no domain events: one appearing here is
/// the failure the count is meant to catch, not something to leave invisible.
/// </para>
/// </remarks>
internal sealed class SaveRecorder : SaveChangesInterceptor
{
    private readonly List<IReadOnlyList<(string Entity, EntityState State)>> _saves = [];

    /// <summary>The pending changes of each save, in order.</summary>
    public IReadOnlyList<IReadOnlyList<(string Entity, EntityState State)>> Saves => _saves;

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Record(eventData);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Record(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Record(DbContextEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context)
        {
            _saves.Add(
            [
                .. context.ChangeTracker.Entries()
                    .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                    .Select(entry => (entry.Entity.GetType().Name, entry.State))
            ]);
        }
    }
}
