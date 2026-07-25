using Ardalis.Specification.EntityFrameworkCore;

using ModulithApp.Features.FeatureName.Domain;

namespace ModulithApp.Features.FeatureName.Infrastructure.Data;

internal sealed class FeatureNameRepository<T>(FeatureNameContext dbContext)
    : RepositoryBase<T>(dbContext), IFeatureNameRepository<T> where T : class;
