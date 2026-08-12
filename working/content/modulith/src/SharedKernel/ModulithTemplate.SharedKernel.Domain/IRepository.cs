using Ardalis.Specification;

namespace ModulithTemplate.SharedKernel.Domain;

public interface IRepository<T> : IRepositoryBase<T> where T : class;
