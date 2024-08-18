using Ardalis.Specification.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;

using ModulithTemplate.FeatureCore;

namespace ModulithTemplate.Infrastructure.Data;

public class EfRepository<T> : RepositoryBase<T>, IRepository<T> where T : class
{
    public EfRepository(CatalogContext dbContext) : base(dbContext) { }

    // Add more constructors here with different dbContext
}
