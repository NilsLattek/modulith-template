using Ardalis.Specification;

namespace ModulithTemplate.FeatureCore;

public interface IRepository<T> : IRepositoryBase<T> where T : class;
