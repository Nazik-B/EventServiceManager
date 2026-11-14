using EventsApi.Domain.Entities;

namespace EventsApi.Application.Repositories;

public interface IUserRepository
{
    Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken = default);
    Task<bool> ExistsByLoginAsync(string login, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
