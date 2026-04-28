using System.Reflection;
using System.Threading.Channels;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.AiPlatform.ValueObjects;
using Atlas.Infrastructure;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services;
using Atlas.Infrastructure.Services.AiPlatform;
using Atlas.Infrastructure.Services.WorkflowEngine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SqlSugar;

#pragma warning disable CS0618 // 测试夹具覆盖旧 JSON 行模型兼容路径。
namespace Atlas.SecurityPlatform.Tests.Services.AiPlatform;

/// <summary>
/// 共用测试装备：基于临时 SQLite 文件 + SqlSugar CodeFirst，按需注入 AiDatabaseService /
/// 仓储 / 仓储伴生服务（IUnitOfWork / IBackgroundWorkQueue stub / IIdGeneratorAccessor / IFileStorageService stub）。
///
/// 注意：仓储基类 <c>RepositoryBase&lt;T&gt;</c> 派生类全部 sealed，无法被 NSubstitute mock，
/// 因此走真实 SQLite + 真实仓储；这是"服务层覆盖"的关键。
/// </summary>
internal sealed class AiDatabaseTestHarness : IDisposable
{
    public static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000099"));

    private readonly string _tempDir;

    public AiDatabaseTestHarness(AiDatabaseQuotaOptions? quotaOptions = null)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"atlas-aidb-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        var dbPath = Path.Combine(_tempDir, "ai-database.db");

        Db = new SqlSugarScope(new ConnectionConfig
        {
            ConnectionString = $"Data Source={dbPath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = ApplyEntityService
            }
        });

        Db.CodeFirst.InitTables(
            typeof(AiDatabase),
            typeof(AiDatabaseField),
            typeof(AiDatabaseChannelConfig),
            typeof(AiDatabaseRecord),
            typeof(AiDatabaseImportTask),
            typeof(AiAppResourceBinding));

        DatabaseRepository = new AiDatabaseRepository(Db);
        FieldRepository = new AiDatabaseFieldRepository(Db);
        ChannelConfigRepository = new AiDatabaseChannelConfigRepository(Db);
        RecordRepository = new AiDatabaseRecordRepository(Db);
        ImportTaskRepository = new AiDatabaseImportTaskRepository(Db);
        BindingRepository = new AiAppResourceBindingRepository(Db);
        UnitOfWork = new SqlSugarUnitOfWork(Db);
        IdGenerator = new IncrementingIdGenerator();
        FileStorage = Substitute.For<IFileStorageService>();
        QuotaPolicy = new AiDatabaseQuotaPolicy(
            Options.Create(quotaOptions ?? new AiDatabaseQuotaOptions { MaxBulkInsertRows = 1000 }),
            DatabaseRepository,
            RecordRepository,
            NullLogger<AiDatabaseQuotaPolicy>.Instance);

        BackgroundWorkQueue = new CapturingBackgroundWorkQueue();
        PhysicalTableService = new AiDatabasePhysicalTableService(Db, NullLogger<AiDatabasePhysicalTableService>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton(DatabaseRepository);
        services.AddSingleton(FieldRepository);
        services.AddSingleton(ChannelConfigRepository);
        services.AddSingleton(RecordRepository);
        services.AddSingleton(ImportTaskRepository);
        services.AddSingleton(BindingRepository);
        services.AddSingleton(QuotaPolicy);
        services.AddSingleton(FileStorage);
        services.AddSingleton<IBackgroundWorkQueue>(BackgroundWorkQueue);
        services.AddSingleton(IdGenerator);
        services.AddSingleton<IUnitOfWork>(UnitOfWork);
        services.AddSingleton(NullLogger<AiDatabaseService>.Instance);
        services.AddSingleton(PhysicalTableService);
        services.AddSingleton<IAiDatabaseProvisioner>(new TestAiDatabaseProvisioner(PhysicalTableService, DatabaseRepository));
        services.AddSingleton<IAiDatabaseService>(sp => new AiDatabaseService(
            DatabaseRepository,
            FieldRepository,
            ChannelConfigRepository,
            RecordRepository,
            ImportTaskRepository,
            BindingRepository,
            QuotaPolicy,
            PhysicalTableService,
            sp.GetRequiredService<IAiDatabaseProvisioner>(),
            FileStorage,
            BackgroundWorkQueue,
            IdGenerator,
            UnitOfWork,
            NullLogger<AiDatabaseService>.Instance));
        ServiceProvider = services.BuildServiceProvider();
        Service = ServiceProvider.GetRequiredService<IAiDatabaseService>();
    }

    public ISqlSugarClient Db { get; }
    public AiDatabaseRepository DatabaseRepository { get; }
    public AiDatabaseFieldRepository FieldRepository { get; }
    public AiDatabaseChannelConfigRepository ChannelConfigRepository { get; }
    public AiDatabaseRecordRepository RecordRepository { get; }
    public AiDatabaseImportTaskRepository ImportTaskRepository { get; }
    public AiAppResourceBindingRepository BindingRepository { get; }
    public AiDatabaseQuotaPolicy QuotaPolicy { get; }
    public CapturingBackgroundWorkQueue BackgroundWorkQueue { get; }
    public AiDatabasePhysicalTableService PhysicalTableService { get; }
    public IIdGeneratorAccessor IdGenerator { get; }
    public IUnitOfWork UnitOfWork { get; }
    public IFileStorageService FileStorage { get; }
    public IServiceProvider ServiceProvider { get; }
    public IAiDatabaseService Service { get; }

    public async Task<AiDatabase> SeedDatabaseAsync(
        string name = "demo",
        string tableSchema = "[{\"name\":\"orderId\",\"type\":\"string\"},{\"name\":\"amount\",\"type\":\"number\"}]",
        AiDatabaseQueryMode queryMode = AiDatabaseQueryMode.MultiUser,
        AiDatabaseChannelScope channelScope = AiDatabaseChannelScope.FullShared,
        long workspaceId = 1L,
        long botId = 1L)
    {
        // SQLite CodeFirst 下对 long? 列可能推断为 NOT NULL（缺省没 DEFAULT），
        // 因此测试里强制赋值；这与生产 AddAsync 的 NULL 用法无关。
        var entity = new AiDatabase(
            Tenant,
            name,
            description: string.Empty,
            botId: botId,
            tableSchema,
            id: IdGenerator.NextId(),
            workspaceId: workspaceId,
            queryMode: queryMode,
            channelScope: channelScope);
        var names = PhysicalTableService.BuildTableNames(Tenant, entity.Id);
        entity.SetPhysicalTables(names.DraftTableName, names.OnlineTableName);
        await DatabaseRepository.AddAsync(entity, CancellationToken.None);
        await PhysicalTableService.EnsureDatabaseTablesAsync(entity, legacyDraftRows: null, CancellationToken.None);
        return entity;
    }

    public NodeExecutionContext BuildNodeContext(
        string nodeKey,
        WorkflowNodeType type,
        Dictionary<string, System.Text.Json.JsonElement> config,
        long? userId = null,
        string? channelId = null,
        bool isDebug = false,
        AiDatabaseRecordEnvironment databaseEnvironment = AiDatabaseRecordEnvironment.Draft)
    {
        var node = new NodeSchema(
            nodeKey,
            type,
            "label",
            config,
            new NodeLayout(0, 0, 100, 60));
        return new NodeExecutionContext(
            node,
            new Dictionary<string, System.Text.Json.JsonElement>(),
            ServiceProvider,
            Tenant,
            workflowId: 1,
            executionId: 1,
            workflowCallStack: Array.Empty<long>(),
            eventChannel: null,
            userId: userId,
            channelId: channelId,
            isDebug: isDebug,
            databaseEnvironment: databaseEnvironment);
    }

    public void Dispose()
    {
        try
        {
            Db.Dispose();
        }
        catch
        {
            // 忽略 dispose 异常
        }
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
            // 测试环境清理失败不阻塞
        }
    }

    /// <summary>
    /// 与 OrmMigrationIntegrationTests 一致的 EntityService：
    ///  - 忽略 TenantId 反射列；
    ///  - 把 long "Id" 标主键（非自增）；
    ///  - 把 Nullable&lt;T&gt; 与 nullable-reference 字符串列显式标 IsNullable=true，避免 SQLite 推成 NOT NULL。
    /// </summary>
    private static void ApplyEntityService(PropertyInfo property, EntityColumnInfo column)
    {
        if (property.Name == nameof(TenantEntity.TenantId)
            && property.PropertyType == typeof(TenantId))
        {
            column.IsIgnore = true;
            return;
        }
        if (property.Name == "Id" && property.PropertyType == typeof(long))
        {
            column.IsPrimarykey = true;
            column.IsIdentity = false;
        }
        if (Nullable.GetUnderlyingType(property.PropertyType) is not null)
        {
            column.IsNullable = true;
        }
        else if (property.PropertyType == typeof(string)
                 && IsNullableReference(property))
        {
            column.IsNullable = true;
        }
    }

    private static readonly NullabilityInfoContext _nullabilityContext = new();

    private static bool IsNullableReference(PropertyInfo property)
    {
        try
        {
            var info = _nullabilityContext.Create(property);
            return info.WriteState == NullabilityState.Nullable
                || info.ReadState == NullabilityState.Nullable;
        }
        catch
        {
            return false;
        }
    }

    private sealed class IncrementingIdGenerator : IIdGeneratorAccessor
    {
        private long _next = 1_000_000;
        public long NextId() => Interlocked.Increment(ref _next);
    }
}

internal sealed class TestAiDatabaseProvisioner : IAiDatabaseProvisioner
{
    private readonly AiDatabasePhysicalTableService _physicalTableService;
    private readonly AiDatabaseRepository _databaseRepository;

    public TestAiDatabaseProvisioner(AiDatabasePhysicalTableService physicalTableService, AiDatabaseRepository databaseRepository)
    {
        _physicalTableService = physicalTableService;
        _databaseRepository = databaseRepository;
    }

    public async Task EnsureProvisionedAsync(AiDatabase database, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(database.DraftTableName) || string.IsNullOrWhiteSpace(database.OnlineTableName))
        {
            var names = _physicalTableService.BuildTableNames(database.TenantId, database.Id);
            database.SetPhysicalTables(names.DraftTableName, names.OnlineTableName);
            await _databaseRepository.UpdateAsync(database, cancellationToken);
        }

        await _physicalTableService.EnsureDatabaseTablesAsync(database, legacyDraftRows: null, cancellationToken);
    }

    public Task EnsureDraftAsync(AiDatabase database, CancellationToken cancellationToken)
        => EnsureProvisionedAsync(database, cancellationToken);

    public Task EnsureOnlineAsync(AiDatabase database, CancellationToken cancellationToken)
        => EnsureProvisionedAsync(database, cancellationToken);

    public Task ValidateHostingOptionsAsync(string driverCode, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task DropAsync(AiDatabase database, CancellationToken cancellationToken)
        => _physicalTableService.DropDatabaseTablesAsync(database, cancellationToken);
}

/// <summary>
/// 测试用 IBackgroundWorkQueue：把 enqueue 的回调缓存下来，让测试主动驱动执行
/// （生产环境是 BackgroundWorkQueueProcessor + ServiceScope 异步处理）。
/// </summary>
internal sealed class CapturingBackgroundWorkQueue : IBackgroundWorkQueue
{
    private readonly List<Func<IServiceProvider, CancellationToken, Task>> _items = new();

    public IReadOnlyList<Func<IServiceProvider, CancellationToken, Task>> Items => _items;

    public void Enqueue(Func<IServiceProvider, CancellationToken, Task> workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        _items.Add(workItem);
    }

    /// <summary>驱动最近一次 enqueue 的工作项；测试通常 Submit 后立即 Drain。</summary>
    public Task DrainLastAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        if (_items.Count == 0)
        {
            throw new InvalidOperationException("没有待执行的后台任务。");
        }
        return _items[^1](serviceProvider, cancellationToken);
    }
}
