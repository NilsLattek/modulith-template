using Ardalis.Specification.EntityFrameworkCore;

using ModulithTemplate.FeatureCore;

namespace ModulithTemplate.Infrastructure.Data;

public class EfRepository<T>(CatalogContext dbContext) : RepositoryBase<T>(dbContext), IRepository<T> where T : class;
