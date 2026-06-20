using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly MongoDbContext _context;
    private readonly DbSet<T> _dbSet;

    public GenericRepository(MongoDbContext context)
    {
        _context = context;
        _dbSet = _context.Set<T>();
    }

    public async Task AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
    }

    public IQueryable<T> AsQueryable()
    {
        return _dbSet.AsQueryable();
    }

    public async Task DeleteAsync(object id)
    {
        var entity = await _dbSet.FindAsync(id);
        if (entity != null)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IList<T>> FindAllAsync(
        Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IQueryable<T>>? include = null)
    {
        IQueryable<T> query = _dbSet;
        if (include != null)
            query = include(query);

        return await query.Where(predicate).ToListAsync();
    }

    public async Task<T?> FindAsync(
        Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IQueryable<T>>? include = null)
    {
        IQueryable<T> query = _dbSet;
        if (include != null)
            query = include(query);

        return await query.FirstOrDefaultAsync(predicate);
    }



    public async Task<BasePaginatedList<object>> GetAllWithPaggingSortSelectionFieldAsync<TEntity, TResponse>(
        IQueryable<TEntity> query,
        IConfigurationProvider mapperConfig,
        string? orderBy = null,
        string? fields = null,
        int pageIndex = 1,
        int pageSize = 10)
    {
        pageIndex = pageIndex < 1 ? 1 : pageIndex;
        pageSize = pageSize < 1 ? 10 : pageSize;
        var validFields = QueryHelper.GetValidFields<TResponse>(fields);
        var validOrderBy = QueryHelper.GetValidOrderBy<TResponse>(orderBy);

        var count = await query.CountAsync();
        if (count == 0) return new BasePaginatedList<object>(new List<object>(), count, pageIndex, pageSize);


        var dtoQuery = query.ProjectTo<TResponse>(mapperConfig);

        if (!string.IsNullOrWhiteSpace(validOrderBy))
        {
            dtoQuery = dtoQuery.OrderBy(validOrderBy);
        }

        var queryWithPaging = dtoQuery.Skip((pageIndex - 1) * pageSize).Take(pageSize);

        if (!string.IsNullOrWhiteSpace(validFields))
        {
            var dynamicItems = await queryWithPaging
                     .Select($"new ({validFields})")
                     .ToDynamicListAsync();

            var serializedItems = dynamicItems.Select(x => (object)x).ToList();

            return new BasePaginatedList<object>(serializedItems, count, pageIndex, pageSize);
        }

        var items = await queryWithPaging.ToListAsync();
        return new BasePaginatedList<object>(items.Cast<object>().ToList(), count, pageIndex, pageSize);

    }

    public async Task<BasePaginatedList<T>> PaginatedListAsync(IQueryable<T> query, int index, int pageSize)
    {
        pageSize = pageSize < 1 ? 10 : pageSize;
        index = index < 1 ? 1 : index;
        var count = await query.CountAsync();
        var items = await query.Skip((index - 1) * pageSize).Take(pageSize).ToListAsync();
        return new BasePaginatedList<T>(items, count, index, pageSize);
    }

    public async Task<T?> GetByIdAsync(object id)
    {
        var entity = await _dbSet.FindAsync(id);
        return entity;
    }

    public Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }
}
