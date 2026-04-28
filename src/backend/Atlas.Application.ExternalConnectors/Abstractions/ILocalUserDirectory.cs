namespace Atlas.Application.ExternalConnectors.Abstractions;

/// <summary>
/// 连接器层用到的本地用户最小查询能力。
/// 由 AppHost 桥接到现有 IUserAccountRepository，避免连接器层反向依赖 Application.Identity 模块。
/// </summary>
public interface ILocalUserDirectory
{
    Task<LocalUserSnapshot?> FindByIdAsync(long localUserId, CancellationToken cancellationToken);

    Task<LocalUserSnapshot?> FindByMobileAsync(string mobile, CancellationToken cancellationToken);

    Task<LocalUserSnapshot?> FindByEmailAsync(string email, CancellationToken cancellationToken);
}

public sealed record LocalUserSnapshot
{
    public required long Id { get; init; }

    public required string Username { get; init; }

    public string? DisplayName { get; init; }

    public string? Email { get; init; }

    public string? Mobile { get; init; }

    public bool IsActive { get; init; }
}
