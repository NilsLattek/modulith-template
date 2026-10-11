namespace ModulithTemplate.SharedKernel.Domain.Entities;

/// <summary>An entity whose stored row carries a version, so a change made from a stale copy of it is refused on save.</summary>
/// <remarks>
/// Opt-in: implement it only where two users or tabs may change the same row and last-write-wins would
/// lose one change. Mapped to Postgres's <c>xmin</c> by <c>ApplySharedModel()</c>; a caller's version is
/// checked by <c>IRepository.ExpectVersion</c>.
/// </remarks>
public interface IVersioned
{
    /// <summary>The version of the row as read, which changes with every save of it; 0 until first saved.</summary>
    uint Version { get; }
}
