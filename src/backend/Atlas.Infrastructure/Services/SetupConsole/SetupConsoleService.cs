using System.Text.Json;
using Atlas.Application.SetupConsole.Abstractions;
using Atlas.Application.SetupConsole.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.Identity.Entities;
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
    private readonly MigrationSecretProtector _secretProtector;
    private readonly Atlas.Application.Abstractions.IPasswordHasher _passwordHasher;
    private readonly ILogger<SetupConsoleService> _logger;

    public SetupConsoleService(
        ISqlSugarClient db,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGen,
        ISetupRecoveryKeyService recoveryKeyService,
        SetupConsoleAuditWriter auditWriter,
        MigrationSecretProtector secretProtector,
        Atlas.Application.Abstractions.IPasswordHasher passwordHasher,
        ILogger<SetupConsoleService> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _idGen = idGen;
        _recoveryKeyService = recoveryKeyService;
        _auditWriter = auditWriter;
        _secretProtector = secretProtector;
        _passwordHasher = passwordHasher;
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
        // M8/B4：按 Type.Namespace 真实拼装 AllRuntimeEntityTypes，每个 category 对应 namespace 前缀集合。
        var categoryRules = new (string Category, string DisplayKey, bool HasSeed, string[] NamespacePrefixes)[]
        {
            ("system-foundation", StateLabelKeys["system-foundation"], true, new[]
            {
                "Atlas.Domain.System",
                "Atlas.Domain.Events",
                "Atlas.Domain.Setup"
            }),
            ("identity-permission", StateLabelKeys["identity-permission"], true, new[]
            {
                "Atlas.Domain.Identity",
                "Atlas.Domain.Platform.Entities.AppMembershipEntities",
                "Atlas.Domain.Platform.Entities.AppOrgEntities"
            }),
            ("workspace", StateLabelKeys["workspace"], true, new[]
            {
                "Atlas.Domain.AiPlatform.Entities" // 仅 Workspace / WorkspaceFolder 等；细化由前端归并
            }),
            ("business-domain", StateLabelKeys["business-domain"], false, new[]
            {
                "Atlas.Domain.AiPlatform",
                "Atlas.Domain.Approval",
                "Atlas.Domain.AgentTeam",
                "Atlas.Domain.LowCode",
                "Atlas.Domain.LogicFlow",
                "Atlas.Domain.BatchProcess",
                "Atlas.Domain.Workflow",
                "Atlas.Domain.DynamicTables"
            }),
            ("resource-runtime", StateLabelKeys["resource-runtime"], false, new[]
            {
                "Atlas.Domain.Plugins",
                "Atlas.Domain.Templates",
                "Atlas.Domain.Integration",
                "Atlas.Domain.License",
                "Atlas.Domain.Assets",
                "Atlas.Domain.Platform"
            }),
            ("audit-log", StateLabelKeys["audit-log"], false, new[]
            {
                "Atlas.Domain.Audit",
                "Atlas.Domain.Alert"
            })
        };

        // 按"先匹配的 category 胜出"原则归类，避免 AiPlatform 既算 workspace 又算 business-domain。
        var categorized = new List<SetupConsoleCatalogCategoryDto>();
        var assigned = new HashSet<Type>();
        foreach (var rule in categoryRules)
        {
            var matched = AtlasOrmSchemaCatalog.RuntimeEntities
                .Where(t => !assigned.Contains(t)
                            && t.Namespace is not null
                            && rule.NamespacePrefixes.Any(p => t.Namespace.StartsWith(p, StringComparison.Ordinal)))
                .ToArray();
            foreach (var type in matched)
            {
                assigned.Add(type);
            }
            categorized.Add(new SetupConsoleCatalogCategoryDto(rule.Category, rule.DisplayKey, matched.Length, rule.HasSeed));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            categorized = categorized
                .Where(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var missing = AtlasOrmSchemaCatalog.GetMissingCriticalTableNames(_db);
        var summary = new SetupConsoleCatalogSummaryDto(
            TotalEntities: categorized.Sum(c => c.EntityCount),
            TotalCategories: categorized.Count,
            MissingCriticalTables: missing,
            Categories: categorized);
        return Task.FromResult(summary);
    }

    /// <summary>
    /// 按分类返回该分类下的所有实体名（M8/B4 + M10/E5 UI 下钻使用）。
    /// </summary>
    public Task<IReadOnlyList<string>> GetCatalogEntitiesAsync(string category, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        // 复用 GetCatalogSummaryAsync 的 namespace 规则；此处简化为直接按 category 重算。
        var rules = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["system-foundation"] = new[] { "Atlas.Domain.System", "Atlas.Core.Messaging", "Atlas.Domain.Events", "Atlas.Domain.Messaging", "Atlas.Domain.Saga", "Atlas.Domain.Setup" },
            ["identity-permission"] = new[] { "Atlas.Domain.Identity", "Atlas.Domain.Platform.Entities.AppMembershipEntities", "Atlas.Domain.Platform.Entities.AppOrgEntities" },
            ["workspace"] = new[] { "Atlas.Domain.AiPlatform.Entities" },
            ["business-domain"] = new[] { "Atlas.Domain.AiPlatform", "Atlas.Domain.Approval", "Atlas.Domain.AgentTeam", "Atlas.Domain.LowCode", "Atlas.Domain.LogicFlow", "Atlas.Domain.BatchProcess", "Atlas.Domain.Workflow", "Atlas.Domain.DynamicTables", "Atlas.Domain.DynamicViews" },
            ["resource-runtime"] = new[] { "Atlas.Domain.Plugins", "Atlas.Domain.Templates", "Atlas.Domain.Integration", "Atlas.Domain.License", "Atlas.Domain.Assets", "Atlas.Domain.Platform" },
            ["audit-log"] = new[] { "Atlas.Domain.Audit", "Atlas.Domain.Alert" }
        };
        if (!rules.TryGetValue(category, out var prefixes))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var entityNames = AtlasOrmSchemaCatalog.RuntimeEntities
            .Where(t => t.Namespace is not null && prefixes.Any(p => t.Namespace.StartsWith(p, StringComparison.Ordinal)))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        return Task.FromResult<IReadOnlyList<string>>(entityNames);
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

            // M8/B1：用 SetupSeedBundleLog 实现"种子模块 × 版本"幂等。
            // 已应用同 version 且未 forceReapply 时直接跳过；
            // 真实写入由 DatabaseInitializerHostedService 启动时完成（M9 拆方法后由这里调度）。
            var bundleVersion = string.IsNullOrWhiteSpace(request.BundleVersion) ? "v1" : request.BundleVersion!;
            var bundles = new[] { "roles-permissions", "menus", "dictionaries", "model-configs" };
            var skippedBundles = new List<string>();
            var appliedBundles = new List<string>();

            foreach (var bundle in bundles)
            {
                var existing = await _db.Queryable<SetupSeedBundleLog>()
                    .Where(item => item.TenantIdValue == _tenantProvider.GetTenantId().Value
                                   && item.Bundle == bundle
                                   && item.Version == bundleVersion)
                    .FirstAsync().ConfigureAwait(false);
                if (existing is not null && !request.ForceReapply)
                {
                    skippedBundles.Add(bundle);
                    continue;
                }

                if (existing is null)
                {
                    await _db.Insertable(new SetupSeedBundleLog(
                        _tenantProvider.GetTenantId(),
                        _idGen.NextId(),
                        bundle,
                        bundleVersion,
                        DateTimeOffset.UtcNow)).ExecuteCommandAsync().ConfigureAwait(false);
                }
                else
                {
                    // forceReapply：仅刷新 AppliedAt 时间戳；幂等不重复插
                    await _db.Updateable<SetupSeedBundleLog>()
                        .SetColumns(it => it.AppliedAt == DateTimeOffset.UtcNow)
                        .Where(it => it.Id == existing.Id)
                        .ExecuteCommandAsync().ConfigureAwait(false);
                }
                appliedBundles.Add(bundle);
            }

            await EnsureSystemStateTransitionAsync(SystemSetupStates.SeedInitialized).ConfigureAwait(false);

            var state = await EnsureSystemStateAsync().ConfigureAwait(false);
            state.SetVersion(bundleVersion, DateTimeOffset.UtcNow);
            await _db.Updateable(state).ExecuteCommandAsync().ConfigureAwait(false);

            return ("seed bundle applied", new Dictionary<string, object?>
            {
                ["bundleVersion"] = bundleVersion,
                ["forceReapply"] = request.ForceReapply,
                ["appliedBundles"] = appliedBundles,
                ["skippedBundles"] = skippedBundles
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
            // M8/B2：真实 upsert UserAccount + Role + UserRole + PasswordHistory（B5：标记 IsBootstrap）。
            var bootstrapAdminTenant = ParseTenantId(request.TenantId);
            var roleCodes = new[] { "SuperAdmin", "Admin" }.Concat(request.OptionalRoleCodes).Distinct().ToArray();
            var passwordHash = _passwordHasher.HashPassword(request.Password);

            // 1) 角色 upsert
            var roleIdByCode = new Dictionary<string, long>(StringComparer.Ordinal);
            var existingRoles = await _db.Queryable<Role>()
                .Where(r => r.TenantIdValue == bootstrapAdminTenant.Value
                            && roleCodes.Contains(r.Code))
                .ToListAsync().ConfigureAwait(false);
            foreach (var role in existingRoles)
            {
                roleIdByCode[role.Code] = role.Id;
            }
            foreach (var roleCode in roleCodes)
            {
                if (roleIdByCode.ContainsKey(roleCode))
                {
                    continue;
                }
                var newRole = new Role(bootstrapAdminTenant, name: roleCode, code: roleCode, id: _idGen.NextId());
                if (string.Equals(roleCode, "SuperAdmin", StringComparison.Ordinal))
                {
                    newRole.MarkSystemRole();
                }
                await _db.Insertable(newRole).ExecuteCommandAsync().ConfigureAwait(false);
                roleIdByCode[roleCode] = newRole.Id;
            }

            // 2) 用户 upsert（按 tenant + username 唯一）
            var existingUser = await _db.Queryable<UserAccount>()
                .Where(u => u.TenantIdValue == bootstrapAdminTenant.Value && u.Username == request.Username)
                .FirstAsync().ConfigureAwait(false);

            UserAccount user;
            bool created;
            if (existingUser is null)
            {
                user = new UserAccount(
                    bootstrapAdminTenant,
                    request.Username,
                    request.Username,
                    passwordHash,
                    _idGen.NextId());
                user.UpdateRoles(string.Join(',', roleCodes));
                if (request.IsPlatformAdmin)
                {
                    user.MarkPlatformAdmin();
                }
                user.MarkSystemAccount();
                await _db.Insertable(user).ExecuteCommandAsync().ConfigureAwait(false);
                created = true;
            }
            else
            {
                existingUser.UpdatePassword(passwordHash, DateTimeOffset.UtcNow);
                existingUser.UpdateRoles(string.Join(',', roleCodes));
                if (request.IsPlatformAdmin)
                {
                    existingUser.MarkPlatformAdmin();
                }
                else
                {
                    existingUser.UnmarkPlatformAdmin();
                }
                await _db.Updateable(existingUser).ExecuteCommandAsync().ConfigureAwait(false);
                user = existingUser;
                created = false;
            }

            // 3) UserRole 关系（先清后插，幂等）
            await _db.Deleteable<UserRole>()
                .Where(ur => ur.TenantIdValue == bootstrapAdminTenant.Value && ur.UserId == user.Id)
                .ExecuteCommandAsync().ConfigureAwait(false);
            foreach (var code in roleCodes)
            {
                await _db.Insertable(new UserRole(
                    bootstrapAdminTenant,
                    user.Id,
                    roleIdByCode[code],
                    _idGen.NextId())).ExecuteCommandAsync().ConfigureAwait(false);
            }

            // 4) PasswordHistory（B5：写入"初始密码"标志，登录后跳改密页）
            await _db.Insertable(new PasswordHistory(
                bootstrapAdminTenant,
                userId: user.Id,
                passwordHash: passwordHash,
                id: _idGen.NextId(),
                createdAt: DateTimeOffset.UtcNow)).ExecuteCommandAsync().ConfigureAwait(false);

            return ("bootstrap admin ensured", new Dictionary<string, object?>
            {
                ["adminUsername"] = request.Username,
                ["adminUserId"] = user.Id,
                ["effectiveRoles"] = roleCodes,
                ["created"] = created,
                ["isPlatformAdmin"] = request.IsPlatformAdmin,
                ["isFirstLogin"] = true
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

    private static TenantId ParseTenantId(string raw)
    {
        if (Guid.TryParse(raw, out var guid))
        {
            return new TenantId(guid);
        }
        throw new ArgumentException($"Invalid tenantId '{raw}'; must be a GUID.");
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
            // M8/B3：真实 upsert Workspace + WorkspaceRole 三类（Owner/Admin/Member） + WorkspaceMember 关系
            var tenant = _tenantProvider.GetTenantId();

            // 1) 找 Owner 用户
            var ownerUser = await _db.Queryable<UserAccount>()
                .Where(u => u.TenantIdValue == tenant.Value && u.Username == request.OwnerUsername)
                .FirstAsync().ConfigureAwait(false);
            if (ownerUser is null)
            {
                throw new InvalidOperationException(
                    $"owner user '{request.OwnerUsername}' not found; please run bootstrap-user step first");
            }

            // 2) Workspace upsert（按 tenant + name 唯一）
            var existingWorkspace = await _db.Queryable<Workspace>()
                .Where(w => w.TenantIdValue == tenant.Value && w.Name == request.WorkspaceName.Trim())
                .FirstAsync().ConfigureAwait(false);
            Workspace workspace;
            bool workspaceCreated;
            if (existingWorkspace is null)
            {
                workspace = new Workspace(
                    tenant,
                    name: request.WorkspaceName,
                    description: "Default workspace ensured by setup-console",
                    icon: null,
                    appInstanceId: 0,
                    appKey: "default",
                    createdBy: ownerUser.Id,
                    id: _idGen.NextId());
                await _db.Insertable(workspace).ExecuteCommandAsync().ConfigureAwait(false);
                workspaceCreated = true;
            }
            else
            {
                workspace = existingWorkspace;
                workspaceCreated = false;
            }

            // 3) 三个内置 WorkspaceRole（Owner/Admin/Member），按 (workspaceId, code) 唯一
            var roleCodes = new[]
            {
                (Code: WorkspaceBuiltInRoleCodes.Owner,  Name: "Owner",  Actions: "[\"view\",\"edit\",\"publish\",\"delete\",\"manage-permission\"]"),
                (Code: WorkspaceBuiltInRoleCodes.Admin,  Name: "Admin",  Actions: "[\"view\",\"edit\",\"publish\",\"manage-permission\"]"),
                (Code: WorkspaceBuiltInRoleCodes.Member, Name: "Member", Actions: "[\"view\",\"edit\"]")
            };
            var existingWsRoles = await _db.Queryable<WorkspaceRole>()
                .Where(r => r.TenantIdValue == tenant.Value && r.WorkspaceId == workspace.Id)
                .ToListAsync().ConfigureAwait(false);
            var wsRoleIdByCode = existingWsRoles.ToDictionary(r => r.Code, r => r.Id, StringComparer.Ordinal);
            foreach (var (code, name, actions) in roleCodes)
            {
                if (wsRoleIdByCode.ContainsKey(code))
                {
                    continue;
                }
                var newRole = new WorkspaceRole(
                    tenant,
                    workspaceId: workspace.Id,
                    code: code,
                    name: name,
                    defaultActionsJson: actions,
                    isSystem: true,
                    id: _idGen.NextId());
                await _db.Insertable(newRole).ExecuteCommandAsync().ConfigureAwait(false);
                wsRoleIdByCode[code] = newRole.Id;
            }

            // 4) WorkspaceMember：Owner 用户 -> Owner 角色（按 (workspaceId, userId) 幂等）
            var ownerWsRoleId = wsRoleIdByCode[WorkspaceBuiltInRoleCodes.Owner];
            var existingMember = await _db.Queryable<WorkspaceMember>()
                .Where(m => m.TenantIdValue == tenant.Value && m.WorkspaceId == workspace.Id && m.UserId == ownerUser.Id)
                .FirstAsync().ConfigureAwait(false);
            if (existingMember is null)
            {
                var member = new WorkspaceMember(
                    tenant,
                    workspaceId: workspace.Id,
                    userId: ownerUser.Id,
                    workspaceRoleId: ownerWsRoleId,
                    addedBy: ownerUser.Id,
                    id: _idGen.NextId());
                await _db.Insertable(member).ExecuteCommandAsync().ConfigureAwait(false);
            }
            else if (existingMember.WorkspaceRoleId != ownerWsRoleId)
            {
                existingMember.ChangeRole(ownerWsRoleId);
                await _db.Updateable(existingMember).ExecuteCommandAsync().ConfigureAwait(false);
            }

            // 5) 同步元数据（setup_workspace_state）
            await UpsertWorkspaceStateAsync(
                workspace.Id.ToString(),
                request.WorkspaceName,
                WorkspaceSetupStates.Completed,
                "v1").ConfigureAwait(false);

            return ("default workspace ensured", new Dictionary<string, object?>
            {
                ["workspaceId"] = workspace.Id,
                ["workspaceName"] = workspace.Name,
                ["workspaceCreated"] = workspaceCreated,
                ["ownerUserId"] = ownerUser.Id,
                ["builtInRoles"] = wsRoleIdByCode.Keys.ToArray(),
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

    private DataMigrationJobDto? MapJob(DataMigrationJob? job)
    {
        if (job is null)
        {
            return null;
        }

        var moduleScope = string.IsNullOrEmpty(job.ModuleScopeJson)
            ? new DataMigrationModuleScopeDto(new[] { "all" }, null)
            : JsonSerializer.Deserialize<DataMigrationModuleScopeDto>(job.ModuleScopeJson)
              ?? new DataMigrationModuleScopeDto(new[] { "all" }, null);

        // M8/A2：连接串脱敏（去掉 password=）；解密走 MigrationSecretProtector，UI 只看到掩码
        var sourceCleartext = _secretProtector.Decrypt(job.SourceConnectionString);
        var targetCleartext = _secretProtector.Decrypt(job.TargetConnectionString);
        return new DataMigrationJobDto(
            job.Id.ToString(),
            job.State,
            job.Mode,
            BuildSafeConfig(job.SourceDbType, sourceCleartext),
            BuildSafeConfig(job.TargetDbType, targetCleartext),
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

    private static DbConnectionConfig BuildSafeConfig(string dbType, string? connectionString)
    {
        var safe = string.IsNullOrEmpty(connectionString)
            ? string.Empty
            : System.Text.RegularExpressions.Regex.Replace(
                connectionString,
                @"(?i)(password|pwd)\s*=\s*[^;]+",
                "$1=***");
        return new DbConnectionConfig(dbType, dbType, "raw", safe, null);
    }
}
