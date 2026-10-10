using Microsoft.EntityFrameworkCore;

using ModulithTemplate.SharedKernel.Domain.Entities;
using ModulithTemplate.SharedKernel.Outbox.Data;

namespace ModulithTemplate.SharedKernel.Infrastructure;

/// <summary>The model configuration every feature context shares.</summary>
public static class SharedModelBuilderExtensions
{
    /// <summary>Maps the shared outbox table, and each <see cref="IVersioned"/> entity's version to Postgres's <c>xmin</c>.</summary>
    /// <remarks>Call it last in <c>OnModelCreating</c>, so it sees every entity type the context maps.</remarks>
    /// <param name="modelBuilder">The model being built, from <c>OnModelCreating</c>.</param>
    /// <returns>The same model builder, for chaining.</returns>
    public static ModelBuilder ApplySharedModel(this ModelBuilder modelBuilder)
    {
        modelBuilder.MapSharedOutbox();

        // xmin is a system column, so no column of its own: a save from a stale copy updates no row.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(type => type.ClrType.IsAssignableTo(typeof(IVersioned))))
        {
            modelBuilder.Entity(entityType.ClrType).Property(nameof(IVersioned.Version)).IsRowVersion();
        }

        return modelBuilder;
    }
}
