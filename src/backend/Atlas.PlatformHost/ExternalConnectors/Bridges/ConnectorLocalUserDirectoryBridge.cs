using Atlas.Application.Abstractions;
using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.PlatformHost.ExternalConnectors.Bridges;

/// <summary>
/// 把 IUserAccountRepository 桥接为 ILocalUserDirectory；按 mobile/email/id 反查本地账户。
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
        // 现有 IUserAccountRepository 没有 FindByPhoneNumber，这里走 keyword 分页查询作为最小可行匹配，
        // 后续可在 IUserAccountRepository 加 FindByPhoneNumberAsync 提升到 SQL 命中。
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
