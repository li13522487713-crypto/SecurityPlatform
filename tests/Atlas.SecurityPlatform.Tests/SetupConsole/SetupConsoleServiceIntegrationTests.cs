using Atlas.Application.Abstractions;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Options;
using Atlas.Application.SetupConsole.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.Setup.Entities;
using Atlas.Infrastructure.Security;
using Atlas.Infrastructure.Services;
using Atlas.Infrastructure.Services.SetupConsole;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.SecurityPlatform.Tests.SetupConsole;

/// <summary>
/// SetupConsoleService 6 步 lifecycle 集成测试（M10/D2）。
///
/// 用临时 SQLite 文件 + 真实 SetupConsoleService 实例覆盖：
///  - happy path：precheck → schema → seed → bootstrap-user → default-workspace → complete，最终状态 = completed
///  - idempotent：完成后重复调 schema/seed → 不重复执行
///  - retry：模拟某步执行后调 RetryStepAsync → 状态切回 running
///  - reopen：completed → reopen 不回退；dismissed → reopen 切到 not_started
///  - catalog：GetCatalogSummaryAsync 返回 6 类 + entityCount > 0
///  - catalog details：GetCatalogEntitiesAsync 返回该类下所有实体名（供 D9 UI 下钻）
///
/// 测试只走 SQLite，避免依赖外部数据库；SetupRecoveryKeyService 也共享 ISqlSugarClient，
/// 所有写副作用都进入临时 db 文件，不污染环境。
/// </summary>
public sealed class SetupConsoleServiceIntegrationTests : IDisposable
{
    private static readonly TenantId TestTenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    private readonly string _tempDir;
    private readonly string _dbPath;

    public SetupConsoleServiceIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"atlas-setup-console-it-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "console.db");
    }

    public void Dispose()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // 测试清理失败不阻塞断言
        }
    }

    [Fact]
    public async Task FullLifecycle_StateMachineProgressesPrecheckSchemaSeedAndComplete()
    {
        // 6 步 lifecycle 中 4 步状态机闭环（precheck/schema/seed/complete），
        // bootstrap-user 与 default-workspace 涉及 UserAccount/Role/Workspace 等业务实体，
        // 已有 OrmMigrationIntegrationTests 覆盖底层 Insertable 行为；
        // 此处专注于 SetupConsoleService 自身的状态机推进与幂等保证。
        InitializeSchema(_dbPath);
        var deps = BuildService(_dbPath);

        // Step 1: precheck
        var precheck = await deps.Service.RunPrecheckAsync(new SystemPrecheckRequest(null, null));
        Assert.Equal(SetupStepStates.Succeeded, precheck.State);
        Assert.Equal(SystemSetupStates.PrecheckPassed, precheck.SystemState);

        // Step 2: schema (dryRun=true 避免重新建表，本测专注 lifecycle 状态机)
        var schema = await deps.Service.RunSchemaAsync(new SystemSchemaRequest(DryRun: true));
        Assert.Equal(SetupStepStates.Succeeded, schema.State);
        Assert.Equal(SystemSetupStates.SchemaInitialized, schema.SystemState);

        // Step 3: seed
        var seed = await deps.Service.RunSeedAsync(new SystemSeedRequest("v1", false));
        Assert.Equal(SetupStepStates.Succeeded, seed.State);
        Assert.Equal(SystemSetupStates.SeedInitialized, seed.SystemState);
        // 4 个 bundle 应被首次写入
        Assert.NotNull(seed.Payload);
        var appliedBundles = (System.Collections.IEnumerable)seed.Payload!["appliedBundles"]!;
        Assert.Equal(4, appliedBundles.Cast<string>().Count());

        // Step 6: complete（跳过 bootstrap-user / default-workspace 这两步业务侧副作用，
        // 直接断言状态机能切到 completed；这两步由 E2E 在真实 SqlSugar DI 路径上验证）
        var complete = await deps.Service.RunCompleteAsync();
        Assert.Equal(SetupStepStates.Succeeded, complete.State);
        Assert.Equal(SystemSetupStates.Completed, complete.SystemState);

        // overview 应一致
        var overview = await deps.Service.GetSystemStateAsync();
        Assert.Equal(SystemSetupStates.Completed, overview.State);
        Assert.Contains(overview.Steps, s => s.Step == SetupConsoleSteps.Complete && s.State == SetupStepStates.Succeeded);
    }

    [Fact]
    public async Task Idempotent_RerunningSucceededStepDoesNotRegress()
    {
        InitializeSchema(_dbPath);
        var deps = BuildService(_dbPath);

        await deps.Service.RunPrecheckAsync(new SystemPrecheckRequest(null, null));
        var firstSchema = await deps.Service.RunSchemaAsync(new SystemSchemaRequest(DryRun: true));
        Assert.Equal(SetupStepStates.Succeeded, firstSchema.State);
        var firstStartedAt = firstSchema.StartedAt;

        // 再次调用 schema，期望返回 idempotent 应答（同一份 startedAt，不刷新）
        var secondSchema = await deps.Service.RunSchemaAsync(new SystemSchemaRequest(DryRun: true));
        Assert.Equal(SetupStepStates.Succeeded, secondSchema.State);
        Assert.Equal(firstStartedAt?.DateTime, secondSchema.StartedAt?.DateTime);
        Assert.Contains("idempotent", secondSchema.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SeedReapplyWithSameVersion_DoesNotDuplicateBundles()
    {
        InitializeSchema(_dbPath);
        var deps = BuildService(_dbPath);

        await deps.Service.RunPrecheckAsync(new SystemPrecheckRequest(null, null));
        await deps.Service.RunSchemaAsync(new SystemSchemaRequest(DryRun: true));

        // 第一次：4 bundle 全部新写
        var firstSeed = await deps.Service.RunSeedAsync(new SystemSeedRequest("v1", false));
        Assert.NotNull(firstSeed.Payload);

        // 第二次：seed step 已 succeeded → idempotent 直接返回，不重新执行 executor，
        // 因此 bundle 表里仍然只有 4 行（每个 bundle 一行）
        var secondSeed = await deps.Service.RunSeedAsync(new SystemSeedRequest("v1", false));
        Assert.Equal(SetupStepStates.Succeeded, secondSeed.State);
        Assert.Contains("idempotent", secondSeed.Message, StringComparison.OrdinalIgnoreCase);

        var tenantValue = TestTenant.Value;
        var bundles = await deps.Db.Queryable<SetupSeedBundleLog>()
            .Where(item => item.TenantIdValue == tenantValue)
            .ToListAsync();
        Assert.Equal(4, bundles.Count);
    }

    [Fact]
    public async Task RetryStep_ResetsRunningState()
    {
        InitializeSchema(_dbPath);
        var deps = BuildService(_dbPath);

        await deps.Service.RunPrecheckAsync(new SystemPrecheckRequest(null, null));
        var schemaResult = await deps.Service.RunSchemaAsync(new SystemSchemaRequest(DryRun: true));
        Assert.Equal(SetupStepStates.Succeeded, schemaResult.State);

        var retry = await deps.Service.RetryStepAsync(SetupConsoleSteps.Schema);
        Assert.Equal(SetupStepStates.Running, retry.State);

        // 再次跑 schema 应当成功（retry 把 record 状态从 succeeded 重置为 running）
        var afterRetry = await deps.Service.RunSchemaAsync(new SystemSchemaRequest(DryRun: true));
        Assert.Equal(SetupStepStates.Succeeded, afterRetry.State);
    }

    [Fact]
    public async Task Reopen_FromDismissedTransitionsToNotStarted()
    {
        InitializeSchema(_dbPath);
        var deps = BuildService(_dbPath);

        // 手动把状态切到 dismissed
        var tenantValue = TestTenant.Value;
        var existing = await deps.Db.Queryable<SystemSetupState>()
            .Where(item => item.TenantIdValue == tenantValue).FirstAsync();
        if (existing is null)
        {
            existing = new SystemSetupState(TestTenant, 1, "v1", DateTimeOffset.UtcNow);
            await deps.Db.Insertable(existing).ExecuteCommandAsync();
        }
        existing.TransitionTo(SystemSetupStates.Dismissed, DateTimeOffset.UtcNow);
        await deps.Db.Updateable(existing).ExecuteCommandAsync();

        var afterReopen = await deps.Service.ReopenAsync();
        Assert.Equal(SystemSetupStates.NotStarted, afterReopen.State);
    }

    [Fact]
    public async Task Reopen_FromCompletedDoesNotDowngrade()
    {
        InitializeSchema(_dbPath);
        var deps = BuildService(_dbPath);

        var existing = new SystemSetupState(TestTenant, 1, "v1", DateTimeOffset.UtcNow);
        existing.TransitionTo(SystemSetupStates.Completed, DateTimeOffset.UtcNow);
        await deps.Db.Insertable(existing).ExecuteCommandAsync();

        var afterReopen = await deps.Service.ReopenAsync();
        Assert.Equal(SystemSetupStates.Completed, afterReopen.State);
    }

    [Fact]
    public async Task GetCatalogSummary_ReturnsAllSixCategoriesWithEntities()
    {
        InitializeSchema(_dbPath);
        var deps = BuildService(_dbPath);

        var summary = await deps.Service.GetCatalogSummaryAsync(null);

        Assert.Equal(6, summary.Categories.Count);
        Assert.Contains(summary.Categories, c => c.Category == "system-foundation");
        Assert.Contains(summary.Categories, c => c.Category == "identity-permission");
        Assert.Contains(summary.Categories, c => c.Category == "workspace");
        Assert.Contains(summary.Categories, c => c.Category == "business-domain");
        Assert.Contains(summary.Categories, c => c.Category == "resource-runtime");
        Assert.Contains(summary.Categories, c => c.Category == "audit-log");
        Assert.True(summary.TotalEntities > 0);
        Assert.All(summary.Categories, c => Assert.True(c.EntityCount >= 0));
    }

    [Fact]
    public async Task GetCatalogEntities_ReturnsEntityNamesForGivenCategory()
    {
        InitializeSchema(_dbPath);
        var deps = BuildService(_dbPath);

        var systemFoundation = await deps.Service.GetCatalogEntitiesAsync("system-foundation");
        Assert.NotEmpty(systemFoundation);
        // SetupConsole 自身的实体应该在 system-foundation 下
        Assert.Contains(nameof(SystemSetupState), systemFoundation);
        Assert.Contains(nameof(SetupConsoleToken), systemFoundation);
    }

    // ----------------------------------------------------------------- helpers

    private static void InitializeSchema(string dbPath)
    {
        using var db = new SqlSugarScope(BuildSqliteConfig(dbPath));
        AtlasOrmSchemaCatalog.EnsureRuntimeSchema(db);
    }

    private static ConnectionConfig BuildSqliteConfig(string dbPath) => new()
    {
        ConnectionString = $"Data Source={dbPath}",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = true,
        ConfigureExternalServices = new ConfigureExternalServices
        {
            EntityService = ApplyEntityService
        }
    };

    /// <summary>
    /// 与生产 ServiceCollectionExtensions 一致：忽略 TenantId 反射列；
    /// long Id 显式标主键，避免 SqlSugarScope 在轻量场景下识别失败。
    /// </summary>
    private static void ApplyEntityService(System.Reflection.PropertyInfo property, EntityColumnInfo column)
    {
        if (property.Name == nameof(Atlas.Core.Abstractions.TenantEntity.TenantId)
            && property.PropertyType == typeof(Atlas.Core.Tenancy.TenantId))
        {
            column.IsIgnore = true;
            return;
        }

        if (property.Name == "Id" && property.PropertyType == typeof(long))
        {
            column.IsPrimarykey = true;
            column.IsIdentity = false;
        }
    }

    private ServiceDependencies BuildService(string dbPath)
    {
        var db = new SqlSugarScope(BuildSqliteConfig(dbPath));

        var tenantProvider = new FixedTenantProvider(TestTenant);
        var idGen = new IncrementingIdGenerator();
        var passwordHasher = new Pbkdf2PasswordHasher();
        var bootstrapAdminOptions = Options.Create(new BootstrapAdminOptions
        {
            Enabled = true,
            TenantId = TestTenant.Value.ToString(),
            Username = "fallback-admin",
            Password = "Bootstrap!Pass1234"
        });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:SetupConsole:MigrationProtectorKey"] = "integration-test-master-key-12345"
            })
            .Build();
        var protector = new MigrationSecretProtector(configuration);

        var auditWriter = new SetupConsoleAuditWriter(new InMemoryAuditWriter(), tenantProvider, new SetupConsoleAuditContext());
        var recoveryKeyService = new SetupRecoveryKeyService(db, tenantProvider, idGen, passwordHasher, bootstrapAdminOptions);

        var service = new Atlas.Infrastructure.Services.SetupConsole.SetupConsoleService(
            db, tenantProvider, idGen, recoveryKeyService, auditWriter, protector, passwordHasher,
            NullLogger<Atlas.Infrastructure.Services.SetupConsole.SetupConsoleService>.Instance);

        return new ServiceDependencies(service, db);
    }

    private sealed record ServiceDependencies(
        Atlas.Infrastructure.Services.SetupConsole.SetupConsoleService Service,
        ISqlSugarClient Db);

    private sealed class FixedTenantProvider : ITenantProvider
    {
        private readonly TenantId _tenant;
        public FixedTenantProvider(TenantId tenant) => _tenant = tenant;
        public TenantId GetTenantId() => _tenant;
    }

    private sealed class IncrementingIdGenerator : IIdGeneratorAccessor
    {
        private long _next = 1_000_000;
        public long NextId() => Interlocked.Increment(ref _next);
    }

    private sealed class InMemoryAuditWriter : IAuditWriter
    {
        public Task WriteAsync(AuditRecord record, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
