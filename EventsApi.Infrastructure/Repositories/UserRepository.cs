using EventsApi.Application.Repositories;
using EventsApi.Domain.Entities;
using EventsApi.Domain.Exceptions;
using EventsApi.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventsApi.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _dbContext;

    public UserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Login == login, cancellationToken);
    }

    public Task<bool> ExistsByLoginAsync(string login, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.AnyAsync(user => user.Login == login, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _dbContext.Users.AddAsync(user, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new UserAlreadyExistsException();
        }
    }
}
