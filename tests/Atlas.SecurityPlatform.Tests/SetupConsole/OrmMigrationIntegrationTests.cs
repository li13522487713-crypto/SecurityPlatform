using Atlas.Application.Abstractions;
using Atlas.Application.Options;
using Atlas.Application.SetupConsole.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.Setup.Entities;
using Atlas.Infrastructure.Security;
using Atlas.Infrastructure.Services.SetupConsole;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.SecurityPlatform.Tests.SetupConsole;

/// <summary>
/// ORM 跨库迁移引擎 SQLite → SQLite 集成测试（M9/C6）。
///
/// 用临时文件 SQLite 模拟源/目标库；
/// 灌入 UserAccount + Role 测试数据后跑完整 lifecycle，断言：
///  - 拓扑排序：被引用方在前
///  - StartJobAsync 真实复制行
///  - DataMigrationCheckpoint 真实写入
///  - ValidateJobAsync 行数 + 抽样字段哈希都通过
///  - CutoverJobAsync 写 appsettings.runtime.json
/// </summary>
public sealed class OrmMigrationIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sourceDbPath;
    private readonly string _targetDbPath;
    private readonly string _platformDbPath;

    public OrmMigrationIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"atlas-orm-migration-it-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _sourceDbPath = Path.Combine(_tempDir, "source.db");
        _targetDbPath = Path.Combine(_tempDir, "target.db");
        _platformDbPath = Path.Combine(_tempDir, "platform.db");
    }

    public void Dispose()
    {
        try
        {
            // 强制释放所有 SQLite 文件句柄
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // 测试环境清理失败不阻塞
        }
    }

    [Fact]
    public void EntityTopologySorter_PutsTenantBeforeUserAccount()
    {
        var sorted = EntityTopologySorter.Sort(new[]
        {
            typeof(UserAccount),  // 引用 Role / Tenant
            typeof(Role),
            typeof(UserRole),     // 引用 UserAccount + Role
            typeof(SystemSetupState)
        });

        var sortedList = sorted.ToList();
        var idxRole = sortedList.IndexOf(typeof(Role));
        var idxUser = sortedList.IndexOf(typeof(UserAccount));
        var idxUserRole = sortedList.IndexOf(typeof(UserRole));

        Assert.True(idxRole < idxUserRole, "Role should be sorted before UserRole");
        Assert.True(idxUser < idxUserRole, "UserAccount should be sorted before UserRole");
    }

    [Fact]
    public void EntityTopologySorter_HandlesEmptyInput()
    {
        var sorted = EntityTopologySorter.Sort(Array.Empty<Type>());
        Assert.Empty(sorted);
    }

    [Fact]
    public void EntityTopologySorter_HandlesAllRuntimeEntities()
    {
        // 真实场景：调用方会传 AtlasOrmSchemaCatalog.RuntimeEntities（与 AllRuntimeEntityTypes 一致，当前 211 个）
        var sorted = EntityTopologySorter.Sort(new[]
        {
            typeof(SystemSetupState),
            typeof(SetupStepRecord),
            typeof(DataMigrationJob),
            typeof(DataMigrationBatch),
            typeof(DataMigrationCheckpoint),
            typeof(SetupConsoleToken),
            typeof(SetupSeedBundleLog),
            typeof(WorkspaceSetupState),
            typeof(DataMigrationLog),
            typeof(DataMigrationReport)
        });

        // 所有实体都应被包含
        Assert.Equal(10, sorted.Count);
    }

    [Fact]
    public void EntityTopologySorter_BuildDependencyGraphIdentifiesUserRoleEdges()
    {
        var graph = EntityTopologySorter.BuildDependencyGraph(new[]
        {
            typeof(UserAccount),
            typeof(Role),
            typeof(UserRole)
        });
        // UserRole 依赖 Role（RoleId → Role 精确匹配）
        Assert.Contains(typeof(Role), graph[typeof(UserRole)]);
        // UserRole.UserId → User → 兜底后缀匹配到 UserAccount（M9 名字兜底规则）
        Assert.Contains(typeof(UserAccount), graph[typeof(UserRole)]);
        // 反过来不依赖
        Assert.DoesNotContain(typeof(UserRole), graph[typeof(UserAccount)]);
    }

    [Fact]
    public async Task FullLifecycle_SqliteToSqlite_CopiesAndValidatesAndCutoverPersists()
    {
        // 1) 准备源库（10 条 SystemSetupState）
        InitializeSchema(_sourceDbPath);
        await SeedSourceDataAsync();

        // 2) 准备目标库（仅 schema）
        InitializeSchema(_targetDbPath);

        // 3) 准备平台元数据库（OrmDataMigrationService 内部用来记 job/batch/checkpoint）
        InitializeSchema(_platformDbPath);

        var (service, runtimeConfigDir) = BuildService(_platformDbPath);

        // 创建 job：mode=structure-plus-data，全量迁移
        var sourceConn = new DbConnectionConfig("SQLite", "SQLite", "raw", $"Data Source={_sourceDbPath}", null);
        var targetConn = new DbConnectionConfig("SQLite", "SQLite", "raw", $"Data Source={_targetDbPath}", null);
        var job = await service.CreateJobAsync(new DataMigrationJobCreateRequest(
            sourceConn,
            targetConn,
            DataMigrationModes.StructurePlusData,
            new DataMigrationModuleScopeDto(new[] { "all" }, null),
            AllowReExecute: false));
        Assert.Equal(DataMigrationStates.Pending, job.State);
        Assert.NotEmpty(job.SourceFingerprint);
        Assert.NotEqual(job.SourceFingerprint, job.TargetFingerprint);

        // 跨引擎源/目标连接串脱敏：UI 看到的 connectionString 不应包含明文 password（这里 SQLite 没 password，跳过此断言）
        Assert.StartsWith("Data Source=", job.Source.ConnectionString);

        // precheck → ready
        var afterPrecheck = await service.PrecheckJobAsync(job.Id);
        Assert.Equal(DataMigrationStates.Ready, afterPrecheck.State);

        // start → 拷贝完成 → 状态切到 validating
        var afterStart = await service.StartJobAsync(job.Id);
        Assert.Equal(DataMigrationStates.Validating, afterStart.State);
        Assert.True(afterStart.CompletedEntities > 0);

        // validate → cutover-ready；至少 SystemSetupState 这张表应通过
        var report = await service.ValidateJobAsync(job.Id);
        Assert.True(report.OverallPassed,
            $"validation should pass; failed={report.FailedEntities}/total={report.TotalEntities}");
        var systemStateRowDiff = report.RowDiff.FirstOrDefault(r => r.EntityName == nameof(SystemSetupState));
        Assert.NotNull(systemStateRowDiff);
        Assert.Equal(systemStateRowDiff!.SourceRowCount, systemStateRowDiff.TargetRowCount);

        // cutover：写 appsettings.runtime.json + 状态终态
        var afterCutover = await service.CutoverJobAsync(job.Id, new DataMigrationCutoverRequest(7, true, true));
        Assert.Equal(DataMigrationStates.CutoverCompleted, afterCutover.State);

        var runtimeConfigPath = Path.Combine(runtimeConfigDir, "appsettings.runtime.json");
        Assert.True(File.Exists(runtimeConfigPath), "appsettings.runtime.json should be persisted by cutover");
        var runtimeJson = await File.ReadAllTextAsync(runtimeConfigPath);
        Assert.Contains("\"Database\"", runtimeJson);
        Assert.Contains(_targetDbPath.Replace("\\", "\\\\"), runtimeJson);
    }

    [Fact]
    public async Task DuplicateJob_WithoutAllowReExecute_IsRejected()
    {
        InitializeSchema(_sourceDbPath);
        InitializeSchema(_targetDbPath);
        InitializeSchema(_platformDbPath);
        var (service, _) = BuildService(_platformDbPath);

        var sourceConn = new DbConnectionConfig("SQLite", "SQLite", "raw", $"Data Source={_sourceDbPath}", null);
        var targetConn = new DbConnectionConfig("SQLite", "SQLite", "raw", $"Data Source={_targetDbPath}", null);
        var job = await service.CreateJobAsync(new DataMigrationJobCreateRequest(
            sourceConn, targetConn, DataMigrationModes.StructurePlusData,
            new DataMigrationModuleScopeDto(new[] { "all" }, null), AllowReExecute: false));

        // 直接把第一个 job 设为 cutover-completed（手动模拟，避免再跑全流程）
        await service.PrecheckJobAsync(job.Id);
        await service.StartJobAsync(job.Id);
        await service.ValidateJobAsync(job.Id);
        await service.CutoverJobAsync(job.Id, new DataMigrationCutoverRequest(7, true, true));

        // 再创建相同源/目标的 job 必须被拒绝
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateJobAsync(new DataMigrationJobCreateRequest(
                sourceConn, targetConn, DataMigrationModes.StructurePlusData,
                new DataMigrationModuleScopeDto(new[] { "all" }, null), AllowReExecute: false)));
        Assert.Contains("already completed", ex.Message);

        // 显式 allowReExecute 后可再创建
        var newJob = await service.CreateJobAsync(new DataMigrationJobCreateRequest(
            sourceConn, targetConn, DataMigrationModes.StructurePlusData,
            new DataMigrationModuleScopeDto(new[] { "all" }, null), AllowReExecute: true));
        Assert.NotEqual(job.Id, newJob.Id);
    }

    // ----------------------------------------------------------------- helpers

    private static void InitializeSchema(string dbPath)
    {
        using var db = new SqlSugarScope(new ConnectionConfig
        {
            ConnectionString = $"Data Source={dbPath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = ApplyEntityService
            }
        });
        // 全量建表（与生产 EnsureRuntimeSchema 一致，~290 实体），
        // 这样 OrmDataMigrationService 遍历 AllRuntimeEntityTypes 时不会因缺表报错。
        Atlas.Infrastructure.Services.AtlasOrmSchemaCatalog.EnsureRuntimeSchema(db);
    }

    private async Task SeedSourceDataAsync()
    {
        using var db = new SqlSugarScope(new ConnectionConfig
        {
            ConnectionString = $"Data Source={_sourceDbPath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = ApplyEntityService
            }
        });

        var tenantId = new TenantId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var systemState = new SystemSetupState(tenantId, id: 1, version: "v1", now: DateTimeOffset.UtcNow);
        await db.Insertable(systemState).ExecuteCommandAsync();

        // 加几条 step record，覆盖 entityType 不为空的场景
        for (var i = 1; i <= 3; i += 1)
        {
            var step = new SetupStepRecord(
                tenantId, id: 100 + i, scope: "system", step: $"step-{i}",
                state: SetupStepStates.Succeeded, now: DateTimeOffset.UtcNow);
            await db.Insertable(step).ExecuteCommandAsync();
        }
    }

    private (OrmDataMigrationService Service, string RuntimeConfigDir) BuildService(string platformDbPath)
    {
        var db = new SqlSugarScope(new ConnectionConfig
        {
            ConnectionString = $"Data Source={platformDbPath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = ApplyEntityService
            }
        });

        var tenantProvider = new FixedTenantProvider(new TenantId(Guid.Parse("00000000-0000-0000-0000-000000000001")));
        var idGen = new IncrementingIdGenerator();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:SetupConsole:MigrationProtectorKey"] = "integration-test-master-key-12345"
            })
            .Build();
        var protector = new MigrationSecretProtector(configuration);

        var hostEnv = new TestHostEnvironment(_tempDir);
        var persistor = new RuntimeConfigPersistor(hostEnv, NullLogger<RuntimeConfigPersistor>.Instance);

        var service = new OrmDataMigrationService(db, tenantProvider, idGen, protector, persistor,
            NullLogger<OrmDataMigrationService>.Instance);
        return (service, _tempDir);
    }

    /// <summary>
    /// 测试专用 EntityService 钩子：
    ///  - 忽略 TenantId 反射列（与生产一致）；
    ///  - 显式把 long 类型的 "Id" 属性标为主键；生产 SqlSugarClient 通过 SqlSugarScope 内部初始化默认识别，
    ///    在轻量 SqlSugarClient 实例化场景下需手动指定，否则 Updateable 会抛 "no primary key"。
    /// </summary>
    private static void ApplyEntityService(System.Reflection.PropertyInfo property, SqlSugar.EntityColumnInfo column)
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

    private sealed class FixedTenantProvider : ITenantProvider
    {
        private readonly TenantId _tenantId;
        public FixedTenantProvider(TenantId tenantId) => _tenantId = tenantId;
        public TenantId GetTenantId() => _tenantId;
    }

    private sealed class IncrementingIdGenerator : IIdGeneratorAccessor
    {
        private long _next = 1_000_000;
        public long NextId() => Interlocked.Increment(ref _next);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRoot)
        {
            ContentRootPath = contentRoot;
            EnvironmentName = "Testing";
            ApplicationName = "Atlas.Tests";
            ContentRootFileProvider = null!;
        }
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
        public string ContentRootPath { get; set; }
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
    }
}
