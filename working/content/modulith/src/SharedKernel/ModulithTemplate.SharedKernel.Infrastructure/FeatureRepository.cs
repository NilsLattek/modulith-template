using Ardalis.Specification.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;

using ModulithTemplate.SharedKernel.Domain;
using ModulithTemplate.SharedKernel.Domain.Entities;
using ModulithTemplate.SharedKernel.Domain.Exceptions;

namespace ModulithTemplate.SharedKernel.Infrastructure;

/// <summary>EF Core implementation of <see cref="IRepository{T}"/> for a feature's own context.</summary>
/// <remarks>A body-less <c>XxxRepository&lt;T&gt;</c> derives from it, taking the feature's context, to bind the feature's marker interface to that context.</remarks>
/// <typeparam name="T">The entity type handled by the repository.</typeparam>
/// <param name="dbContext">The feature's context.</param>
public abstract class FeatureRepository<T>(DbContext dbContext) : RepositoryBase<T>(dbContext), IRepository<T>
    where T : class
{
    /// <inheritdoc />
    public void ExpectVersion(IVersioned entity, uint version) =>
        DbContext.Entry(entity).Property(nameof(IVersioned.Version)).OriginalValue = version;

    /// <summary>Saves, reporting a row whose version changed since it was read as a <see cref="ConcurrencyException"/>.</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException(ex.Message, ex);
        }
    }
}
