using Ardalis.Specification;

namespace ModulithTemplate.Domain.Common;

public interface IRepository<T> : IRepositoryBase<T> where T : class;
