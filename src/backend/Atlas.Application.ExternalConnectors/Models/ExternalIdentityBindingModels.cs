using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Application.ExternalConnectors.Models;

public sealed class ExternalIdentityBindingResponse
{
    public long Id { get; set; }

    public long ProviderId { get; set; }

    public long LocalUserId { get; set; }

    public string ExternalUserId { get; set; } = string.Empty;

    public string? OpenId { get; set; }

    public string? UnionId { get; set; }

    public string? Mobile { get; set; }

    public string? Email { get; set; }

    public IdentityBindingStatus Status { get; set; }

    public IdentityBindingMatchStrategy MatchStrategy { get; set; }

    public string Source { get; set; } = string.Empty;

    public DateTimeOffset BoundAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}

public sealed class ExternalIdentityBindingListItem
{
    public long Id { get; set; }

    public long ProviderId { get; set; }

    public long LocalUserId { get; set; }

    public string ExternalUserId { get; set; } = string.Empty;

    public IdentityBindingStatus Status { get; set; }

    public IdentityBindingMatchStrategy MatchStrategy { get; set; }

    public DateTimeOffset BoundAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}

public sealed class ManualBindingRequest
{
    public long ProviderId { get; set; }

    public long LocalUserId { get; set; }

    public string ExternalUserId { get; set; } = string.Empty;

    public string? OpenId { get; set; }

    public string? UnionId { get; set; }

    public string? Mobile { get; set; }

    public string? Email { get; set; }
}

public sealed class BindingConflictResolutionRequest
{
    public long BindingId { get; set; }

    public BindingConflictResolution Resolution { get; set; }

    /// <summary>若选择 SwitchToLocalUser，则填入新的本地用户 ID。</summary>
    public long? NewLocalUserId { get; set; }
}

public enum BindingConflictResolution
{
    /// <summary>保持现状，关闭冲突标记。</summary>
    KeepCurrent = 1,
    /// <summary>切换到指定的本地用户 ID。</summary>
    SwitchToLocalUser = 2,
    /// <summary>解绑当前 binding（不影响其他 provider 的绑定）。</summary>
    Revoke = 3,
}
