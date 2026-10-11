using Ardalis.Specification;

using ModulithTemplate.SharedKernel.Domain.Entities;

namespace ModulithTemplate.SharedKernel.Domain;

public interface IRepository<T> : IRepositoryBase<T> where T : class
{
    /// <summary>Has the next save refuse to change or remove <paramref name="entity"/> unless its row is still at <paramref name="version"/>.</summary>
    /// <remarks>The refusal is a <see cref="Exceptions.ConcurrencyException"/> from <c>SaveChangesAsync</c>.</remarks>
    /// <param name="entity">A tracked entity, loaded by this repository's context.</param>
    /// <param name="version">The version the caller last saw of it.</param>
    void ExpectVersion(IVersioned entity, uint version);
}
