using Ardalis.Specification.EntityFrameworkCore;

using ModulithTemplate.Features.Payments.Domain;

namespace ModulithTemplate.Features.Payments.Infrastructure.Data;

internal sealed class PaymentsRepository<T>(PaymentsContext dbContext)
    : RepositoryBase<T>(dbContext), IPaymentsRepository<T> where T : class;
