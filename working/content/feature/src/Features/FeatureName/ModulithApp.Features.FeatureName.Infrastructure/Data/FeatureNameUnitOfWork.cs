using ModulithApp.Features.FeatureName.Domain;
using ModulithApp.SharedKernel.Infrastructure;

namespace ModulithApp.Features.FeatureName.Infrastructure.Data;

internal sealed class FeatureNameUnitOfWork(FeatureNameContext dbContext)
    : UnitOfWorkBase<FeatureNameContext>(dbContext), IFeatureNameUnitOfWork;
