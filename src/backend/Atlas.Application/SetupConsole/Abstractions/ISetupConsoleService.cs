using Atlas.Application.SetupConsole.Models;

namespace Atlas.Application.SetupConsole.Abstractions;

/// <summary>
/// 系统初始化与迁移控制台服务（M5）。
///
/// 实现要点：
///  - 状态机持久化在 <c>SystemSetupState</c> + <c>WorkspaceSetupState</c> 两张表；
///  - 每步落 <c>SetupStepRecord</c> 记录，幂等保证已 succeeded 不重复执行写库副作用；
///  - 完成 <c>BootstrapUser</c> 时（GenerateRecoveryKey=true）一次性返回明文 RecoveryKey，
///    密文用 PBKDF2 写 <c>SystemSetupState.RecoveryKeyHash</c>。
/// </summary>
public interface ISetupConsoleService
{
    Task<SetupConsoleOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<SystemSetupStateDto> GetSystemStateAsync(CancellationToken cancellationToken = default);

    Task<SetupConsoleCatalogSummaryDto> GetCatalogSummaryAsync(string? category = null, CancellationToken cancellationToken = default);

    Task<SetupStepResultDto> RunPrecheckAsync(SystemPrecheckRequest request, CancellationToken cancellationToken = default);
    Task<SetupStepResultDto> RunSchemaAsync(SystemSchemaRequest request, CancellationToken cancellationToken = default);
    Task<SetupStepResultDto> RunSeedAsync(SystemSeedRequest request, CancellationToken cancellationToken = default);
    Task<SystemBootstrapUserResponse> RunBootstrapUserAsync(SystemBootstrapUserRequest request, CancellationToken cancellationToken = default);
    Task<SetupStepResultDto> RunDefaultWorkspaceAsync(SystemDefaultWorkspaceRequest request, CancellationToken cancellationToken = default);
    Task<SetupStepResultDto> RunCompleteAsync(CancellationToken cancellationToken = default);

    Task<SetupStepResultDto> RetryStepAsync(string step, CancellationToken cancellationToken = default);
    Task<SystemSetupStateDto> ReopenAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceSetupStateDto>> ListWorkspacesAsync(CancellationToken cancellationToken = default);
    Task<WorkspaceSetupStateDto> InitializeWorkspaceAsync(string workspaceId, WorkspaceInitRequest request, CancellationToken cancellationToken = default);
    Task<WorkspaceSetupStateDto> ApplyWorkspaceSeedBundleAsync(string workspaceId, WorkspaceSeedBundleRequest request, CancellationToken cancellationToken = default);
    Task<WorkspaceSetupStateDto> CompleteWorkspaceInitAsync(string workspaceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 控制台二次认证服务（M5）。
///
/// - 颁发独立于 JWT 的 <c>ConsoleToken</c>（30 分钟过期）
/// - 校验恢复密钥 / BootstrapAdmin 凭证（任一通过即可）
/// - <c>SetupConsoleAuthMiddleware</c> 在 setup 已完成的运行时仍放行控制台路径
/// </summary>
public interface ISetupRecoveryKeyService
{
    Task<ConsoleAuthTokenDto?> AuthenticateAsync(ConsoleAuthChallengeRequest request, CancellationToken cancellationToken = default);
    Task<ConsoleAuthTokenDto?> RefreshAsync(string consoleToken, CancellationToken cancellationToken = default);
    Task<bool> ValidateAsync(string consoleToken, CancellationToken cancellationToken = default);
    Task RevokeAsync(string consoleToken, CancellationToken cancellationToken = default);

    /// <summary>用于 BootstrapUser 阶段一次性生成并存储 hash；明文返回给调用方一次。</summary>
    Task<string> GenerateAndPersistRecoveryKeyAsync(CancellationToken cancellationToken = default);
}
