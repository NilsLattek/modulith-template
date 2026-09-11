using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ModulithTemplate.Features.Payments.Domain.Entities;

namespace ModulithTemplate.Features.Payments.Infrastructure.Data.Configurations;

/// <summary>
/// Maps <see cref="Payment"/> into the Payments schema.
/// </summary>
/// <remarks>
/// Discovered by <c>ApplyConfigurationsFromAssembly</c> in <see cref="PaymentsContext"/>, so an
/// entity is mapped by adding a file here — the context never changes. Table and column names come
/// from the snake_case convention, so only what it cannot infer is spelled out.
/// </remarks>
internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(payment => payment.Id);

        // Explicit, because Npgsql maps an unconfigured decimal to unconstrained `numeric`, which
        // would store a finer amount than the entity permits.
        builder.Property(payment => payment.Amount)
            .HasPrecision(Payment.AmountPrecision, Payment.AmountScale);

        // One payment per order is not assumed; the index serves the lookup by order.
        builder.HasIndex(payment => payment.OrderId);
    }
}
