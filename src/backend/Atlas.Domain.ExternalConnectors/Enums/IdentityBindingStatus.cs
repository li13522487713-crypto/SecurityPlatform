namespace Atlas.Domain.ExternalConnectors.Enums;

public enum IdentityBindingStatus
{
    /// <summary>已绑定且生效。</summary>
    Active = 1,

    /// <summary>等待用户/管理员确认（如手机号匹配但需要管理员复核）。</summary>
    PendingConfirm = 2,

    /// <summary>检测到冲突（重名 / 同手机号绑定到不同本地账号），需要人工处理。</summary>
    Conflict = 3,

    /// <summary>已解绑/撤销。</summary>
    Revoked = 4,
}
