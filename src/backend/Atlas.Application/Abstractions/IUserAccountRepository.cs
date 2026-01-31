using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Abstractions;

public interface IUserAccountRepository
{
    Task<UserAccount?> FindByUsernameAsync(TenantId tenantId, string username, CancellationToken cancellationToken);
    Task<UserAccount?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<UserAccount> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken);
    Task<(IReadOnlyList<UserAccount> Items, int TotalCount)> QueryPageByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> userIds,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken);
    Task<bool> ExistsByUsernameAsync(TenantId tenantId, string username, CancellationToken cancellationToken);
    Task AddAsync(UserAccount account, CancellationToken cancellationToken);
    Task UpdateAsync(UserAccount account, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}
