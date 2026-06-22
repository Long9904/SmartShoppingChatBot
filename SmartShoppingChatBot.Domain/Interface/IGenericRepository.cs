using AutoMapper;
using SmartShoppingChatBot.Domain.Commons;
using System.Linq.Expressions;

namespace SmartShoppingChatBot.Domain.Interface;

public interface IGenericRepository<T> where T : class
{
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(object id);
    IQueryable<T> AsQueryable();
    Task<T?> FindAsync(Expression<Func<T, bool>> predicate,
            Func<IQueryable<T>, IQueryable<T>>? include = null);

    Task<IList<T>> FindAllAsync(
           Expression<Func<T, bool>> predicate,
           Func<IQueryable<T>, IQueryable<T>>? include = null);

    Task<BasePaginatedList<object>> GetAllWithPaggingSortSelectionFieldAsync<TEntity, TResponse>(
            IQueryable<TEntity> query,
            IConfigurationProvider mapperConfig,
            string? orderBy = null,
            string? fields = null,
            int pageIndex = 1,
            int pageSize = 10);

    Task<BasePaginatedList<T>> PaginatedListAsync(IQueryable<T> query, int index, int pageSize);

    Task<T?> GetByIdAsync(object id);

}
