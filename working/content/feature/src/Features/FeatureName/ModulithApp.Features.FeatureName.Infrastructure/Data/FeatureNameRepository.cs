using ModulithApp.Features.FeatureName.Domain;
using ModulithApp.SharedKernel.Infrastructure;

namespace ModulithApp.Features.FeatureName.Infrastructure.Data;

internal sealed class FeatureNameRepository<T>(FeatureNameContext dbContext)
    : FeatureRepository<T>(dbContext), IFeatureNameRepository<T> where T : class;
