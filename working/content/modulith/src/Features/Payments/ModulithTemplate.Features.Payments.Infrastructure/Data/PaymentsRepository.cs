using ModulithTemplate.Features.Payments.Domain;
using ModulithTemplate.SharedKernel.Infrastructure;

namespace ModulithTemplate.Features.Payments.Infrastructure.Data;

internal sealed class PaymentsRepository<T>(PaymentsContext dbContext)
    : FeatureRepository<T>(dbContext), IPaymentsRepository<T> where T : class;
