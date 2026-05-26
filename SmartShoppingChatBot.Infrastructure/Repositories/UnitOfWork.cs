using Microsoft.EntityFrameworkCore.Storage;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly MongoDbContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(MongoDbContext context)
    {
        _context = context;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
            _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            _transaction.Dispose();
            _transaction = null;
        }
    }

    public async Task RollBackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            _transaction.Dispose();
            _transaction = null;
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
