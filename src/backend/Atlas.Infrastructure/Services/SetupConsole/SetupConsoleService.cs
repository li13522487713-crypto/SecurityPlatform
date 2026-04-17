using System.Text.Json;
using Atlas.Application.SetupConsole.Abstractions;
using Atlas.Application.SetupConsole.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Setup.Entities;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services.SetupConsole;

/// <summary>
/// 系统初始化与迁移控制台服务（M5）。
///
/// 设计原则：
/// - 状态机持久化：<c>SystemSetupState</c>（每租户单例）+ <c>WorkspaceSetupState</c>（每空间）+ <c>SetupStepRecord</c>（每步）。
/// - 幂等：每个 RunXxxAsync 进入前先取 / upsert <c>SystemSetupState</c>，重复调用已 succeeded 步骤不重复执行写副作用。
/// - schema 步骤真实调用 <see cref="AtlasOrmSchemaCatalog.EnsureRuntimeSchema"/>；其它步骤当前阶段仅写 <see cref="SetupStepRecord"/>，
///   M7 接入现有种子服务后即可真正落库。
/// - 完成 BootstrapUser 时通过 <see cref="ISetupRecoveryKeyService.GenerateAndPersistRecoveryKeyAsync"/> 一次性返回明文恢复密钥。
/// </summary>
public sealed class SetupConsoleService : ISetupConsoleService
{
    private const string DefaultWorkspaceId = "default";

    private static readonly Dictionary<string, string> StateLabelKeys = new(StringComparer.Ordinal)
    {
        ["system-foundation"] = "setupConsoleCatalogCategorySystemFoundation",
        ["identity-permission"] = "setupConsoleCatalogCategoryIdentityPermission",
        ["workspace"] = "setupConsoleCatalogCategoryWorkspace",
        ["business-domain"] = "setupConsoleCatalogCategoryBusinessDomain",
        ["resource-runtime"] = "setupConsoleCatalogCategoryResourceRuntime",
        ["audit-log"] = "setupConsoleCatalogCategoryAuditLog"
    };

    private readonly ISqlSugarClient _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly ISetupRecoveryKeyService _recoveryKeyService;
    private readonly SetupConsoleAuditWriter _auditWriter;
    private readonly ILogger<SetupConsoleService> _logger;

    public SetupConsoleService(
        ISqlSugarClient db,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGen,
        ISetupRecoveryKeyService recoveryKeyService,
        SetupConsoleAuditWriter auditWriter,
        ILogger<SetupConsoleService> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _idGen = idGen;
        _recoveryKeyService = recoveryKeyService;
        _auditWriter = auditWriter;
        _logger = logger;
    }

    public async Task<SetupConsoleOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var system = await GetSystemStateAsync(cancellationToken).ConfigureAwait(false);
        var workspaces = await ListWorkspacesAsync(cancellationToken).ConfigureAwait(false);
        var catalog = await GetCatalogSummaryAsync(null, cancellationToken).ConfigureAwait(false);

        var activeJob = await _db.Queryable<DataMigrationJob>()
            .Where(item => item.TenantIdValue == _tenantProvider.GetTenantId().Value)
            .Where(item => item.State != DataMigrationStates.CutoverCompleted
                           && item.State != DataMigrationStates.RolledBack)
            .OrderByDescending(item => item.UpdatedAt)
            .FirstAsync()
            .ConfigureAwait(false);

        return new SetupConsoleOverviewDto(system, workspaces, MapJob(activeJob), catalog);
    }

    public async Task<SystemSetupStateDto> GetSystemStateAsync(CancellationToken cancellationToken = default)
    {
        var state = await EnsureSystemStateAsync().ConfigureAwait(false);
        var steps = await ListSystemStepsAsync().ConfigureAwait(false);
        return new SystemSetupStateDto(
            state.State,
            state.Version,
            state.LastUpdatedAt,
            state.FailureMessage,
            state.RecoveryKeyConfigured,
            steps);
    }

    public Task<SetupConsoleCatalogSummaryDto> GetCatalogSummaryAsync(string? category = null, CancellationToken cancellationToken = default)
    {
        // M5 阶段：固定 6 大类聚合，与前端 mock 一致。
        // M6 后改为读 AtlasOrmSchemaCatalog.RuntimeEntities 真实拼装并按 [SugarTable] 模块归类。
        var categories = new List<SetupConsoleCatalogCategoryDto>
        {
            new("system-foundation", StateLabelKeys["system-foundation"], 10, true),
            new("identity-permission", StateLabelKeys["identity-permission"], 22, true),
            new("workspace", StateLabelKeys["workspace"], 14, true),
            new("business-domain", StateLabelKeys["business-domain"], 192, false),
            new("resource-runtime", StateLabelKeys["resource-runtime"], 18, false),
            new("audit-log", StateLabelKeys["audit-log"], 10, false)
        };

        if (!string.IsNullOrWhiteSpace(category))
        {
            categories = categories.Where(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var missing = AtlasOrmSchemaCatalog.GetMissingCriticalTableNames(_db);
        var summary = new SetupConsoleCatalogSummaryDto(
            TotalEntities: categories.Sum(c => c.EntityCount),
            TotalCategories: categories.Count,
            MissingCriticalTables: missing,
            Categories: categories);
        return Task.FromResult(summary);
    }

    public async Task<SetupStepResultDto> RunPrecheckAsync(SystemPrecheckRequest request, CancellationToken cancellationToken = default)
    {
        _ = request;
        return await RunStepAsync(SetupConsoleSteps.Precheck, async _ =>
        {
            await EnsureSystemStateTransitionAsync(SystemSetupStates.PrecheckPassed).ConfigureAwait(false);
            return ("precheck passed", new Dictionary<string, object?>
            {
                ["dryRun"] = false
            });
        }).ConfigureAwait(false);
    }

    public async Task<SetupStepResultDto> RunSchemaAsync(SystemSchemaRequest request, CancellationToken cancellationToken = default)
    {
        return await RunStepAsync(SetupConsoleSteps.Schema, async _ =>
        {
            await EnsureSystemStateTransitionAsync(SystemSetupStates.SchemaInitializing).ConfigureAwait(false);

            if (!request.DryRun)
            {
                _logger.LogInformation("[SetupConsole] running EnsureRuntimeSchema (real)");
                AtlasOrmSchemaCatalog.EnsureRuntimeSchema(_db);
            }

            await EnsureSystemStateTransitionAsync(SystemSetupStates.SchemaInitialized).ConfigureAwait(false);
            return ("schema initialized", new Dictionary<string, object?>
            {
                ["tablesCreated"] = AtlasOrmSchemaCatalog.RuntimeEntities.Count,
                ["dryRun"] = request.DryRun
            });
        }).ConfigureAwait(false);
    }

    public async Task<SetupStepResultDto> RunSeedAsync(SystemSeedRequest request, CancellationToken cancellationToken = default)
    {
        return await RunStepAsync(SetupConsoleSteps.Seed, async _ =>
        {
            await EnsureSystemStateTransitionAsync(SystemSetupStates.SeedInitializing).ConfigureAwait(false);
            // M5 阶段：种子由现有 DatabaseInitializerHostedService 在系统启动时已执行一次；
            // 控制台只是承认这一事实，写一条 succeeded 步骤记录。
            // M7 阶段：拆出 6 个 public 幂等方法后由这里调度，支持版本化补种（forceReapply）。
            await EnsureSystemStateTransitionAsync(SystemSetupStates.SeedInitialized).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(request.BundleVersion))
            {
                var state = await EnsureSystemStateAsync().ConfigureAwait(false);
                state.SetVersion(request.BundleVersion!, DateTimeOffset.UtcNow);
                await _db.Updateable(state).ExecuteCommandAsync().ConfigureAwait(false);
            }

            return ("seed bundle applied", new Dictionary<string, object?>
            {
                ["bundleVersion"] = request.BundleVersion ?? "v1",
                ["forceReapply"] = request.ForceReapply
            });
        }).ConfigureAwait(false);
    }

    public async Task<SystemBootstrapUserResponse> RunBootstrapUserAsync(SystemBootstrapUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("username/password are required", nameof(request));
        }

        var stepResult = await RunStepAsync(SetupConsoleSteps.BootstrapUser, async _ =>
        {
            // M5 阶段：BootstrapAdmin 用户在 DatabaseInitializerHostedService 启动时已确保存在；
            // 这里仅做 step 记录 + 可选生成恢复密钥。M7 阶段补"按 username 真创建/更新"。
            return ("bootstrap admin acknowledged", new Dictionary<string, object?>
            {
                ["adminUsername"] = request.Username,
                ["effectiveRoles"] = new[] { "SuperAdmin", "Admin" }.Concat(request.OptionalRoleCodes).Distinct().ToArray()
            });
        }).ConfigureAwait(false);

        string? recoveryKey = null;
        if (request.GenerateRecoveryKey)
        {
            recoveryKey = await _recoveryKeyService.GenerateAndPersistRecoveryKeyAsync(cancellationToken).ConfigureAwait(false);
        }

        return new SystemBootstrapUserResponse(
            stepResult.Step,
            stepResult.State,
            stepResult.Message,
            stepResult.SystemState,
            stepResult.StartedAt,
            stepResult.EndedAt,
            recoveryKey,
            stepResult.Payload);
    }

    public async Task<SetupStepResultDto> RunDefaultWorkspaceAsync(SystemDefaultWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.WorkspaceName) || string.IsNullOrWhiteSpace(request.OwnerUsername))
        {
            throw new ArgumentException("workspaceName/ownerUsername are required", nameof(request));
        }

        return await RunStepAsync(SetupConsoleSteps.DefaultWorkspace, async _ =>
        {
            await UpsertWorkspaceStateAsync(
                DefaultWorkspaceId,
                request.WorkspaceName,
                WorkspaceSetupStates.Completed,
                "v1").ConfigureAwait(false);

            return ("default workspace ensured", new Dictionary<string, object?>
            {
                ["workspaceId"] = DefaultWorkspaceId,
                ["workspaceName"] = request.WorkspaceName,
                ["defaultPublishChannelsApplied"] = request.ApplyDefaultPublishChannels,
                ["defaultModelStubApplied"] = request.ApplyDefaultModelStub
            });
        }).ConfigureAwait(false);
    }

    public async Task<SetupStepResultDto> RunCompleteAsync(CancellationToken cancellationToken = default)
    {
        return await RunStepAsync(SetupConsoleSteps.Complete, async _ =>
        {
            await EnsureSystemStateTransitionAsync(SystemSetupStates.Completed).ConfigureAwait(false);
            return ("system initialization completed", null);
        }).ConfigureAwait(false);
    }

    public async Task<SetupStepResultDto> RetryStepAsync(string step, CancellationToken cancellationToken = default)
    {
        var record = await _db.Queryable<SetupStepRecord>()
            .Where(item => item.TenantIdValue == _tenantProvider.GetTenantId().Value)
            .Where(item => item.Scope == "system" && item.Step == step)
            .FirstAsync().ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        if (record is null)
        {
            record = new SetupStepRecord(
                _tenantProvider.GetTenantId(),
                _idGen.NextId(),
                "system",
                step,
                SetupStepStates.Running,
                now);
            await _db.Insertable(record).ExecuteCommandAsync().ConfigureAwait(false);
        }
        else
        {
            record.Restart(now);
            await _db.Updateable(record).ExecuteCommandAsync().ConfigureAwait(false);
        }

        var sysState = await EnsureSystemStateAsync().ConfigureAwait(false);
        return new SetupStepResultDto(
            step,
            SetupStepStates.Running,
            $"retry triggered for {step}",
            sysState.State,
            now,
            null);
    }

    public async Task<SystemSetupStateDto> ReopenAsync(CancellationToken cancellationToken = default)
    {
        var state = await EnsureSystemStateAsync().ConfigureAwait(false);
        if (state.State == SystemSetupStates.Dismissed)
        {
            state.TransitionTo(SystemSetupStates.NotStarted, DateTimeOffset.UtcNow);
            await _db.Updateable(state).ExecuteCommandAsync().ConfigureAwait(false);
        }
        return await GetSystemStateAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WorkspaceSetupStateDto>> ListWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        var tenantValue = _tenantProvider.GetTenantId().Value;
        var rows = await _db.Queryable<WorkspaceSetupState>()
            .Where(item => item.TenantIdValue == tenantValue)
            .OrderBy(item => item.WorkspaceId)
            .ToListAsync()
            .ConfigureAwait(false);

        if (rows.Count == 0)
        {
            // 默认行：保证 UI 永远有 default 这一条
            await UpsertWorkspaceStateAsync(DefaultWorkspaceId, "Default workspace", WorkspaceSetupStates.Pending, "v0").ConfigureAwait(false);
            rows = await _db.Queryable<WorkspaceSetupState>()
                .Where(item => item.TenantIdValue == tenantValue)
                .OrderBy(item => item.WorkspaceId)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        return rows
            .Select(row => new WorkspaceSetupStateDto(
                row.WorkspaceId,
                row.WorkspaceName,
                row.State,
                row.SeedBundleVersion,
                row.LastUpdatedAt))
            .ToList();
    }

    public async Task<WorkspaceSetupStateDto> InitializeWorkspaceAsync(
        string workspaceId,
        WorkspaceInitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new ArgumentException("workspaceId is required", nameof(workspaceId));
        }
        if (string.IsNullOrWhiteSpace(request.WorkspaceName))
        {
            throw new ArgumentException("workspaceName is required", nameof(request));
        }

        var existing = await GetWorkspaceStateAsync(workspaceId).ConfigureAwait(false);
        if (existing is not null && existing.State == WorkspaceSetupStates.Completed)
        {
            return ToDto(existing);
        }

        await UpsertWorkspaceStateAsync(
            workspaceId,
            request.WorkspaceName,
            WorkspaceSetupStates.Completed,
            request.SeedBundleVersion).ConfigureAwait(false);

        var refreshed = await GetWorkspaceStateAsync(workspaceId).ConfigureAwait(false);
        return ToDto(refreshed!);
    }

    public async Task<WorkspaceSetupStateDto> ApplyWorkspaceSeedBundleAsync(
        string workspaceId,
        WorkspaceSeedBundleRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetWorkspaceStateAsync(workspaceId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"workspace {workspaceId} not found");

        if (existing.SeedBundleVersion == request.BundleVersion && !request.ForceReapply)
        {
            return ToDto(existing);
        }

        existing.SetSeedBundleVersion(request.BundleVersion, DateTimeOffset.UtcNow);
        existing.TransitionTo(WorkspaceSetupStates.Completed, DateTimeOffset.UtcNow);
        await _db.Updateable(existing).ExecuteCommandAsync().ConfigureAwait(false);
        return ToDto(existing);
    }

    public async Task<WorkspaceSetupStateDto> CompleteWorkspaceInitAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var existing = await GetWorkspaceStateAsync(workspaceId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"workspace {workspaceId} not found");

        existing.TransitionTo(WorkspaceSetupStates.Completed, DateTimeOffset.UtcNow);
        await _db.Updateable(existing).ExecuteCommandAsync().ConfigureAwait(false);
        return ToDto(existing);
    }

    // ---------------------------------------------------------------- internals

    private async Task<SystemSetupState> EnsureSystemStateAsync()
    {
        var tenant = _tenantProvider.GetTenantId();
        var state = await _db.Queryable<SystemSetupState>()
            .Where(item => item.TenantIdValue == tenant.Value)
            .FirstAsync().ConfigureAwait(false);
        if (state is not null)
        {
            return state;
        }

        var fresh = new SystemSetupState(tenant, _idGen.NextId(), "v1", DateTimeOffset.UtcNow);
        await _db.Insertable(fresh).ExecuteCommandAsync().ConfigureAwait(false);
        return fresh;
    }

    private async Task EnsureSystemStateTransitionAsync(string nextState)
    {
        var state = await EnsureSystemStateAsync().ConfigureAwait(false);
        if (state.State == nextState)
        {
            return;
        }
        state.TransitionTo(nextState, DateTimeOffset.UtcNow);
        await _db.Updateable(state).ExecuteCommandAsync().ConfigureAwait(false);
    }

    private async Task<List<SetupStepRecordDto>> ListSystemStepsAsync()
    {
        var tenant = _tenantProvider.GetTenantId();
        var rows = await _db.Queryable<SetupStepRecord>()
            .Where(item => item.TenantIdValue == tenant.Value && item.Scope == "system")
            .OrderBy(item => item.StartedAt)
            .ToListAsync()
            .ConfigureAwait(false);
        return rows
            .Select(row => new SetupStepRecordDto(
                row.Step,
                row.State,
                row.StartedAt,
                row.EndedAt,
                row.AttemptCount,
                row.ErrorMessage))
            .ToList();
    }

    private async Task<WorkspaceSetupState?> GetWorkspaceStateAsync(string workspaceId)
    {
        var tenant = _tenantProvider.GetTenantId();
        return await _db.Queryable<WorkspaceSetupState>()
            .Where(item => item.TenantIdValue == tenant.Value && item.WorkspaceId == workspaceId)
            .FirstAsync()
            .ConfigureAwait(false);
    }

    private async Task UpsertWorkspaceStateAsync(string workspaceId, string workspaceName, string state, string seedBundleVersion)
    {
        var tenant = _tenantProvider.GetTenantId();
        var now = DateTimeOffset.UtcNow;
        var existing = await GetWorkspaceStateAsync(workspaceId).ConfigureAwait(false);
        if (existing is null)
        {
            var fresh = new WorkspaceSetupState(tenant, _idGen.NextId(), workspaceId, workspaceName, seedBundleVersion, now);
            fresh.TransitionTo(state, now);
            await _db.Insertable(fresh).ExecuteCommandAsync().ConfigureAwait(false);
            return;
        }

        existing.SetWorkspaceName(workspaceName, now);
        existing.SetSeedBundleVersion(seedBundleVersion, now);
        existing.TransitionTo(state, now);
        await _db.Updateable(existing).ExecuteCommandAsync().ConfigureAwait(false);
    }

    private static WorkspaceSetupStateDto ToDto(WorkspaceSetupState row)
        => new(row.WorkspaceId, row.WorkspaceName, row.State, row.SeedBundleVersion, row.LastUpdatedAt);

    private async Task<SetupStepResultDto> RunStepAsync(
        string step,
        Func<SetupStepRecord, Task<(string Message, IDictionary<string, object?>? Payload)>> executor)
    {
        var tenant = _tenantProvider.GetTenantId();
        var now = DateTimeOffset.UtcNow;
        var existing = await _db.Queryable<SetupStepRecord>()
            .Where(item => item.TenantIdValue == tenant.Value && item.Scope == "system" && item.Step == step)
            .FirstAsync().ConfigureAwait(false);

        if (existing is null)
        {
            existing = new SetupStepRecord(tenant, _idGen.NextId(), "system", step, SetupStepStates.Running, now);
            await _db.Insertable(existing).ExecuteCommandAsync().ConfigureAwait(false);
        }
        else if (existing.State == SetupStepStates.Succeeded)
        {
            // 幂等：已 succeeded 的步骤直接返回历史结果，不重复执行 executor 副作用。
            var sysSnapshot = await EnsureSystemStateAsync().ConfigureAwait(false);
            return new SetupStepResultDto(
                step,
                SetupStepStates.Succeeded,
                "step already completed; idempotent acknowledgement",
                sysSnapshot.State,
                existing.StartedAt,
                existing.EndedAt);
        }
        else
        {
            existing.Restart(now);
            await _db.Updateable(existing).ExecuteCommandAsync().ConfigureAwait(false);
        }

        try
        {
            var (message, payload) = await executor(existing).ConfigureAwait(false);
            existing.MarkSucceeded(DateTimeOffset.UtcNow, payload is null ? null : JsonSerializer.Serialize(payload));
            await _db.Updateable(existing).ExecuteCommandAsync().ConfigureAwait(false);

            var sysState = await EnsureSystemStateAsync().ConfigureAwait(false);
            await _auditWriter.WriteAsync($"step.{step}", target: $"system:{sysState.State}", message: message)
                .ConfigureAwait(false);
            return new SetupStepResultDto(
                step,
                SetupStepStates.Succeeded,
                message,
                sysState.State,
                existing.StartedAt,
                existing.EndedAt,
                payload);
        }
        catch (Exception ex)
        {
            existing.MarkFailed(DateTimeOffset.UtcNow, ex.Message);
            await _db.Updateable(existing).ExecuteCommandAsync().ConfigureAwait(false);

            var failState = await EnsureSystemStateAsync().ConfigureAwait(false);
            failState.TransitionTo(SystemSetupStates.Failed, DateTimeOffset.UtcNow, ex.Message);
            await _db.Updateable(failState).ExecuteCommandAsync().ConfigureAwait(false);

            await _auditWriter
                .WriteAsync($"step.{step}", target: $"system:{failState.State}", result: "Failed", message: ex.Message)
                .ConfigureAwait(false);
            _logger.LogError(ex, "[SetupConsole] step {Step} failed", step);
            throw;
        }
    }

    private static DataMigrationJobDto? MapJob(DataMigrationJob? job)
    {
        if (job is null)
        {
            return null;
        }

        var moduleScope = string.IsNullOrEmpty(job.ModuleScopeJson)
            ? new DataMigrationModuleScopeDto(new[] { "all" }, null)
            : JsonSerializer.Deserialize<DataMigrationModuleScopeDto>(job.ModuleScopeJson)
              ?? new DataMigrationModuleScopeDto(new[] { "all" }, null);

        return new DataMigrationJobDto(
            job.Id.ToString(),
            job.State,
            job.Mode,
            new DbConnectionConfig(job.SourceDbType, job.SourceDbType, "raw", job.SourceConnectionString, null),
            new DbConnectionConfig(job.TargetDbType, job.TargetDbType, "raw", job.TargetConnectionString, null),
            job.SourceFingerprint,
            job.TargetFingerprint,
            moduleScope,
            job.TotalEntities,
            job.CompletedEntities,
            job.FailedEntities,
            job.TotalRows,
            job.CopiedRows,
            job.ProgressPercent,
            job.CurrentEntityName,
            job.CurrentBatchNo,
            job.StartedAt,
            job.FinishedAt,
            job.ErrorSummary,
            job.CreatedAt,
            job.UpdatedAt);
    }
}
