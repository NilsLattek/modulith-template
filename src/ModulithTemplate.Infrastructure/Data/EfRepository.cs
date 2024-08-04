using Ardalis.Specification.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;

using ModulithTemplate.FeatureCore;

namespace ModulithTemplate.Infrastructure.Data;

public class EfRepository<T>(DbContext dbContext) : RepositoryBase<T>(dbContext), IRepository<T> where T : class;
