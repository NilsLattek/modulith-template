using ModulithTemplate.Features.Payments.Domain;
using ModulithTemplate.SharedKernel.Infrastructure;

namespace ModulithTemplate.Features.Payments.Infrastructure.Data;

internal sealed class PaymentsUnitOfWork(PaymentsContext dbContext)
    : UnitOfWorkBase<PaymentsContext>(dbContext), IPaymentsUnitOfWork;
