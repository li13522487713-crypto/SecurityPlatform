using Atlas.Application.Abstractions;
using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.AppHost.ExternalConnectors.Bridges;

/// <summary>
/// 把 IUserAccountRepository 桥接为 ILocalUserDirectory；按 mobile/email/id 反查本地账户。
/// AppHost 端复用同一套 SqlSugar 仓储，绑定查询结果与 AppHost 业务用户一致。
/// </summary>
public sealed class ConnectorLocalUserDirectoryBridge : ILocalUserDirectory
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly ITenantProvider _tenantProvider;

    public ConnectorLocalUserDirectoryBridge(IUserAccountRepository userAccountRepository, ITenantProvider tenantProvider)
    {
        _userAccountRepository = userAccountRepository;
        _tenantProvider = tenantProvider;
    }

    public async Task<LocalUserSnapshot?> FindByIdAsync(long localUserId, CancellationToken cancellationToken)
    {
        var account = await _userAccountRepository.FindByIdAsync(_tenantProvider.GetTenantId(), localUserId, cancellationToken).ConfigureAwait(false);
        return account is null ? null : Map(account);
    }

    public async Task<LocalUserSnapshot?> FindByMobileAsync(string mobile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mobile))
        {
            return null;
        }
        var (items, _) = await _userAccountRepository.QueryPageAsync(_tenantProvider.GetTenantId(), 1, 5, mobile, cancellationToken).ConfigureAwait(false);
        var hit = items.FirstOrDefault(a => string.Equals(a.PhoneNumber, mobile, StringComparison.OrdinalIgnoreCase));
        return hit is null ? null : Map(hit);
    }

    public async Task<LocalUserSnapshot?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }
        var account = await _userAccountRepository.FindByEmailAsync(_tenantProvider.GetTenantId(), email, cancellationToken).ConfigureAwait(false);
        return account is null ? null : Map(account);
    }

    private static LocalUserSnapshot Map(Atlas.Domain.Identity.Entities.UserAccount account)
        => new()
        {
            Id = account.Id,
            Username = account.Username,
            DisplayName = account.DisplayName,
            Email = account.Email,
            Mobile = account.PhoneNumber,
            IsActive = account.IsActive,
        };
}
