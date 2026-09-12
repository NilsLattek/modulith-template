using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ModulithTemplate.Features.Orders.Domain.Entities;

namespace ModulithTemplate.Features.Orders.Infrastructure.Data.Configurations;

/// <summary>
/// Maps <see cref="SomeEntity"/> into the Orders schema.
/// </summary>
/// <remarks>
/// Discovered by <c>ApplyConfigurationsFromAssembly</c> in <see cref="OrdersContext"/>, so an entity
/// is mapped by adding a file here — the context never changes. Table and column names come from the
/// snake_case convention, so only what it cannot infer is spelled out.
/// </remarks>
internal sealed class SomeEntityConfiguration : IEntityTypeConfiguration<SomeEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SomeEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name)
            .IsRequired()
            .HasMaxLength(SomeEntity.NameMaxLength);
        builder.Property(entity => entity.Amount)
            .HasPrecision(SomeEntity.AmountPrecision, SomeEntity.AmountScale);
    }
}
