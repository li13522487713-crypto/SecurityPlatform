using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
/// ORM 优先的跨库数据迁移服务（M6）。
///
/// 实施要点：
/// - 源库 / 目标库各 new <see cref="SqlSugarClient"/> 跨实例独立，避免污染主库 ScopedClient；
/// - 用 reflection 遍历 <see cref="AtlasOrmSchemaCatalog.RuntimeEntities"/>，对每个实体做 Queryable&lt;TEntity&gt;.ToList → Insertable(rows).ExecuteCommand；
/// - 状态机沿用 <see cref="DataMigrationStates"/>；
/// - 防重复指纹：基于源 / 目标连接串 + DbType 计算 SHA256；
/// - 当前阶段（M6）：核心结构 + 同引擎/跨引擎 ORM 路径就位；M7 强化"抽样字段比对 + 切主写 appsettings.runtime.json"。
/// </summary>
public sealed class OrmDataMigrationService : IDataMigrationOrmService
{
    private const int DefaultBatchSize = 500;

    private readonly ISqlSugarClient _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly ILogger<OrmDataMigrationService> _logger;

    public OrmDataMigrationService(
        ISqlSugarClient db,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGen,
        ILogger<OrmDataMigrationService> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _idGen = idGen;
        _logger = logger;
    }

    public async Task<MigrationTestConnectionResponse> TestConnectionAsync(
        DbConnectionConfig connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        try
        {
            using var scope = OpenScope(connection);
            var ok = await scope.Ado.GetScalarAsync("SELECT 1") is not null;
            var tableCount = ok ? scope.DbMaintenance.GetTableInfoList(false).Count : 0;
            return new MigrationTestConnectionResponse(
                Connected: ok,
                Message: ok ? "connection successful" : "connection failed",
                DetectedDbType: connection.DbType,
                DetectedTableCount: tableCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OrmMigration] TestConnection failed for {DbType}", connection.DbType);
            return new MigrationTestConnectionResponse(
                Connected: false,
                Message: ex.Message,
                DetectedDbType: connection.DbType,
                DetectedTableCount: 0);
        }
    }

    public async Task<DataMigrationJobDto> CreateJobAsync(
        DataMigrationJobCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourceFingerprint = ComputeFingerprint(request.Source);
        var targetFingerprint = ComputeFingerprint(request.Target);
        var tenantValue = _tenantProvider.GetTenantId().Value;

        var existing = await _db.Queryable<DataMigrationJob>()
            .Where(item => item.TenantIdValue == tenantValue
                           && item.SourceFingerprint == sourceFingerprint
                           && item.TargetFingerprint == targetFingerprint
                           && item.State == DataMigrationStates.CutoverCompleted)
            .FirstAsync()
            .ConfigureAwait(false);

        if (existing is not null && !request.AllowReExecute)
        {
            throw new InvalidOperationException(
                "an identical migration has already completed; set allowReExecute=true to re-run");
        }

        var now = DateTimeOffset.UtcNow;
        var job = new DataMigrationJob(
            _tenantProvider.GetTenantId(),
            id: _idGen.NextId(),
            mode: request.Mode,
            sourceConnectionString: request.Source.ConnectionString ?? string.Empty,
            sourceDbType: request.Source.DbType,
            targetConnectionString: request.Target.ConnectionString ?? string.Empty,
            targetDbType: request.Target.DbType,
            sourceFingerprint: sourceFingerprint,
            targetFingerprint: targetFingerprint,
            moduleScopeJson: JsonSerializer.Serialize(request.ModuleScope),
            createdBy: 0,
            now: now);
        await _db.Insertable(job).ExecuteCommandAsync().ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "OrmDataMigrationService", $"job created mode={request.Mode}", null).ConfigureAwait(false);
        return MapJob(job);
    }

    public async Task<DataMigrationJobDto> PrecheckJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        job.TransitionTo(DataMigrationStates.Prechecking, DateTimeOffset.UtcNow);
        await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);

        var sourceCfg = BuildSourceConfig(job);
        var targetCfg = BuildTargetConfig(job);

        var sourceTest = await TestConnectionAsync(sourceCfg, cancellationToken).ConfigureAwait(false);
        var targetTest = await TestConnectionAsync(targetCfg, cancellationToken).ConfigureAwait(false);

        if (!sourceTest.Connected || !targetTest.Connected)
        {
            job.TransitionTo(DataMigrationStates.Failed, DateTimeOffset.UtcNow,
                $"connectivity failed: source={sourceTest.Message} / target={targetTest.Message}");
            await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
            await AppendLogAsync(job.Id, "error", "Precheck", "connectivity failed", null).ConfigureAwait(false);
            return MapJob(job);
        }

        job.TransitionTo(DataMigrationStates.Ready, DateTimeOffset.UtcNow);
        await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "Precheck", "passed", null).ConfigureAwait(false);
        return MapJob(job);
    }

    public async Task<DataMigrationJobDto> StartJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        if (job.State is not (DataMigrationStates.Ready or DataMigrationStates.Failed or DataMigrationStates.RolledBack))
        {
            throw new InvalidOperationException($"cannot start migration from state {job.State}");
        }

        var entityTypes = AtlasOrmSchemaCatalog.RuntimeEntities;
        job.MarkRunning(entityTypes.Count, totalRows: 0, now: DateTimeOffset.UtcNow);
        await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "Start", $"running with {entityTypes.Count} entities", null).ConfigureAwait(false);

        var sourceCfg = BuildSourceConfig(job);
        var targetCfg = BuildTargetConfig(job);

        long totalCopied = 0;
        var completedCount = 0;
        var failedCount = 0;

        // M6 同步执行（mock 任务量小）；真生产场景 M7 转 Hangfire 后台。
        try
        {
            using var sourceScope = OpenScope(sourceCfg);
            using var targetScope = OpenScope(targetCfg);

            // 仅 StructureOnly 和 StructurePlusData 在目标库初始化结构；ValidateOnly 不动结构。
            if (job.Mode is DataMigrationModes.StructureOnly or DataMigrationModes.StructurePlusData
                or DataMigrationModes.ReExecute)
            {
                AtlasOrmSchemaCatalog.EnsureRuntimeSchema(targetScope);
                await AppendLogAsync(job.Id, "info", "Schema", "target schema ensured", null).ConfigureAwait(false);
            }

            if (job.Mode is DataMigrationModes.StructureOnly)
            {
                completedCount = entityTypes.Count;
            }
            else
            {
                foreach (var entityType in entityTypes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var copied = await CopyEntityAsync(sourceScope, targetScope, job.Id, entityType, cancellationToken)
                        .ConfigureAwait(false);
                    totalCopied += copied;
                    completedCount += 1;

                    job.RecordProgress(
                        currentEntity: entityType.Name,
                        currentBatch: 1,
                        completedEntities: completedCount,
                        failedEntities: failedCount,
                        copiedRows: totalCopied,
                        now: DateTimeOffset.UtcNow);
                    await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
                }
            }

            job.RecordProgress(
                currentEntity: null!,
                currentBatch: 0,
                completedEntities: completedCount,
                failedEntities: failedCount,
                copiedRows: totalCopied,
                now: DateTimeOffset.UtcNow);
            job.TransitionTo(DataMigrationStates.Validating, DateTimeOffset.UtcNow);
            await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
            await AppendLogAsync(job.Id, "info", "Start", $"copied {totalCopied} rows across {completedCount} entities", null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            job.TransitionTo(DataMigrationStates.Failed, DateTimeOffset.UtcNow, ex.Message);
            await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
            await AppendLogAsync(job.Id, "error", "Start", ex.Message, null).ConfigureAwait(false);
            _logger.LogError(ex, "[OrmMigration] job {JobId} failed", job.Id);
            throw;
        }

        return MapJob(job);
    }

    public async Task<DataMigrationProgressDto> GetProgressAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        var batches = await _db.Queryable<DataMigrationBatch>()
            .Where(item => item.TenantIdValue == _tenantProvider.GetTenantId().Value && item.JobId == job.Id)
            .OrderByDescending(item => item.BatchNo)
            .Take(5)
            .ToListAsync()
            .ConfigureAwait(false);

        return new DataMigrationProgressDto(
            JobId: job.Id.ToString(),
            State: job.State,
            TotalEntities: job.TotalEntities,
            CompletedEntities: job.CompletedEntities,
            FailedEntities: job.FailedEntities,
            TotalRows: job.TotalRows,
            CopiedRows: job.CopiedRows,
            ProgressPercent: job.ProgressPercent,
            CurrentEntityName: job.CurrentEntityName,
            CurrentBatchNo: job.CurrentBatchNo,
            UpdatedAt: job.UpdatedAt,
            RecentBatches: batches.Select(b => new DataMigrationBatchDto(
                b.BatchNo,
                b.EntityName,
                b.RowsCopied,
                b.State,
                b.StartedAt,
                b.EndedAt,
                b.Checksum)).ToList());
    }

    public async Task<DataMigrationReportDto> ValidateJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        if (job.State is not (DataMigrationStates.Validating or DataMigrationStates.Running or DataMigrationStates.Ready))
        {
            throw new InvalidOperationException($"cannot validate migration from state {job.State}");
        }

        var sourceCfg = BuildSourceConfig(job);
        var targetCfg = BuildTargetConfig(job);
        using var sourceScope = OpenScope(sourceCfg);
        using var targetScope = OpenScope(targetCfg);

        var rowDiff = new List<DataMigrationRowDiffDto>();
        var passed = 0;
        var failed = 0;

        foreach (var entityType in AtlasOrmSchemaCatalog.RuntimeEntities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var sourceCount = (long)CountEntity(sourceScope, entityType);
                var targetCount = (long)CountEntity(targetScope, entityType);
                rowDiff.Add(new DataMigrationRowDiffDto(entityType.Name, sourceCount, targetCount, sourceCount - targetCount));
                if (sourceCount == targetCount)
                {
                    passed += 1;
                }
                else
                {
                    failed += 1;
                }
            }
            catch (Exception ex)
            {
                failed += 1;
                await AppendLogAsync(job.Id, "warn", "Validate", $"{entityType.Name}: {ex.Message}", entityType.Name).ConfigureAwait(false);
            }
        }

        var overallPassed = failed == 0;
        var report = new DataMigrationReport(
            _tenantProvider.GetTenantId(),
            id: _idGen.NextId(),
            jobId: job.Id,
            totalEntities: AtlasOrmSchemaCatalog.RuntimeEntities.Count,
            passedEntities: passed,
            failedEntities: failed,
            overallPassed: overallPassed,
            rowDiffJson: JsonSerializer.Serialize(rowDiff),
            samplingDiffJson: "[]",
            now: DateTimeOffset.UtcNow);
        await _db.Insertable(report).ExecuteCommandAsync().ConfigureAwait(false);

        if (overallPassed)
        {
            job.TransitionTo(DataMigrationStates.CutoverReady, DateTimeOffset.UtcNow);
        }
        else
        {
            job.TransitionTo(DataMigrationStates.Failed, DateTimeOffset.UtcNow, $"{failed} entities failed validation");
        }
        await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "Validate", $"passed={passed}/{AtlasOrmSchemaCatalog.RuntimeEntities.Count}", null).ConfigureAwait(false);

        return new DataMigrationReportDto(
            JobId: job.Id.ToString(),
            TotalEntities: report.TotalEntities,
            PassedEntities: report.PassedEntities,
            FailedEntities: report.FailedEntities,
            RowDiff: rowDiff,
            SamplingDiff: Array.Empty<DataMigrationSamplingDiffDto>(),
            OverallPassed: overallPassed,
            GeneratedAt: report.GeneratedAt);
    }

    public async Task<DataMigrationJobDto> CutoverJobAsync(
        string jobId,
        DataMigrationCutoverRequest request,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        if (job.State is not (DataMigrationStates.CutoverReady or DataMigrationStates.Validating))
        {
            throw new InvalidOperationException($"cannot cutover from state {job.State}");
        }
        job.MarkFinished(DataMigrationStates.CutoverCompleted, DateTimeOffset.UtcNow);
        await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "Cutover", $"completed; keep source readonly for {request.KeepSourceReadonlyForDays} days", null).ConfigureAwait(false);
        return MapJob(job);
    }

    public async Task<DataMigrationJobDto> RollbackJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        if (job.State == DataMigrationStates.CutoverCompleted)
        {
            throw new InvalidOperationException("cannot rollback an already cutover-completed migration");
        }
        job.MarkFinished(DataMigrationStates.RolledBack, DateTimeOffset.UtcNow);
        await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
        await AppendLogAsync(job.Id, "warn", "Rollback", "rolled back by user request", null).ConfigureAwait(false);
        return MapJob(job);
    }

    public async Task<DataMigrationJobDto> RetryJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await LoadJobAsync(jobId).ConfigureAwait(false);
        if (job.State is not (DataMigrationStates.Failed or DataMigrationStates.RolledBack))
        {
            throw new InvalidOperationException($"cannot retry from state {job.State}");
        }
        job.TransitionTo(DataMigrationStates.Ready, DateTimeOffset.UtcNow);
        await _db.Updateable(job).ExecuteCommandAsync().ConfigureAwait(false);
        await AppendLogAsync(job.Id, "info", "Retry", "retry triggered; ready to resume", null).ConfigureAwait(false);
        return MapJob(job);
    }

    public async Task<DataMigrationReportDto?> GetReportAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        var jobLong = ParseJobId(jobId);
        var report = await _db.Queryable<DataMigrationReport>()
            .Where(item => item.TenantIdValue == _tenantProvider.GetTenantId().Value && item.JobId == jobLong)
            .OrderByDescending(item => item.GeneratedAt)
            .FirstAsync()
            .ConfigureAwait(false);
        if (report is null)
        {
            return null;
        }

        var rowDiff = JsonSerializer.Deserialize<List<DataMigrationRowDiffDto>>(report.RowDiffJson) ?? new List<DataMigrationRowDiffDto>();
        var samplingDiff = JsonSerializer.Deserialize<List<DataMigrationSamplingDiffDto>>(report.SamplingDiffJson) ?? new List<DataMigrationSamplingDiffDto>();

        return new DataMigrationReportDto(
            JobId: jobId,
            TotalEntities: report.TotalEntities,
            PassedEntities: report.PassedEntities,
            FailedEntities: report.FailedEntities,
            RowDiff: rowDiff,
            SamplingDiff: samplingDiff,
            OverallPassed: report.OverallPassed,
            GeneratedAt: report.GeneratedAt);
    }

    public async Task<DataMigrationLogPagedResponse> GetLogsAsync(
        string jobId,
        string? level,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var jobLong = ParseJobId(jobId);
        var tenant = _tenantProvider.GetTenantId().Value;
        var query = _db.Queryable<DataMigrationLog>()
            .Where(item => item.TenantIdValue == tenant && item.JobId == jobLong);
        if (!string.IsNullOrWhiteSpace(level))
        {
            query = query.Where(item => item.Level == level);
        }

        var total = await query.CountAsync().ConfigureAwait(false);
        var page = Math.Max(pageIndex, 1);
        var size = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 200);
        var items = await query
            .OrderByDescending(item => item.OccurredAt)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync()
            .ConfigureAwait(false);

        return new DataMigrationLogPagedResponse(
            Items: items.Select(item => new DataMigrationLogItemDto(
                item.Id.ToString(),
                jobId,
                item.Level,
                item.Module,
                item.EntityName,
                item.Message,
                item.OccurredAt)).ToList(),
            Total: total,
            PageIndex: page,
            PageSize: size);
    }

    // ---------------------------------------------------------------- internals

    private async Task<DataMigrationJob> LoadJobAsync(string jobId)
    {
        var jobLong = ParseJobId(jobId);
        var tenant = _tenantProvider.GetTenantId().Value;
        var job = await _db.Queryable<DataMigrationJob>()
            .Where(item => item.TenantIdValue == tenant && item.Id == jobLong)
            .FirstAsync()
            .ConfigureAwait(false);
        return job ?? throw new InvalidOperationException($"migration job {jobId} not found");
    }

    private async Task<long> CopyEntityAsync(
        SqlSugarClient sourceScope,
        SqlSugarClient targetScope,
        long jobId,
        Type entityType,
        CancellationToken cancellationToken)
    {
        var batchNo = 1;
        var totalCopied = 0L;
        var skip = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = QueryPage(sourceScope, entityType, skip, DefaultBatchSize);
            if (rows is null || rows.Count == 0)
            {
                break;
            }

            try
            {
                InsertBatch(targetScope, entityType, rows);
                totalCopied += rows.Count;

                var batch = new DataMigrationBatch(
                    _tenantProvider.GetTenantId(),
                    id: _idGen.NextId(),
                    jobId: jobId,
                    entityName: entityType.Name,
                    batchNo: batchNo,
                    now: DateTimeOffset.UtcNow);
                batch.MarkSucceeded(rows.Count, checksum: null, now: DateTimeOffset.UtcNow);
                await _db.Insertable(batch).ExecuteCommandAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var batch = new DataMigrationBatch(
                    _tenantProvider.GetTenantId(),
                    id: _idGen.NextId(),
                    jobId: jobId,
                    entityName: entityType.Name,
                    batchNo: batchNo,
                    now: DateTimeOffset.UtcNow);
                batch.MarkFailed(ex.Message, DateTimeOffset.UtcNow);
                await _db.Insertable(batch).ExecuteCommandAsync().ConfigureAwait(false);
                await AppendLogAsync(jobId, "error", "Copy", $"{entityType.Name}#{batchNo}: {ex.Message}", entityType.Name).ConfigureAwait(false);
                throw;
            }

            if (rows.Count < DefaultBatchSize)
            {
                break;
            }
            skip += DefaultBatchSize;
            batchNo += 1;
        }

        // 写 checkpoint（最后位置）
        if (totalCopied > 0)
        {
            var checkpoint = new DataMigrationCheckpoint(
                _tenantProvider.GetTenantId(),
                id: _idGen.NextId(),
                jobId: jobId,
                entityName: entityType.Name,
                now: DateTimeOffset.UtcNow);
            checkpoint.Advance(batchNo, lastMaxId: 0, rowsCopied: totalCopied, now: DateTimeOffset.UtcNow);
            await _db.Insertable(checkpoint).ExecuteCommandAsync().ConfigureAwait(false);
        }

        return totalCopied;
    }

    private static System.Collections.IList? QueryPage(SqlSugarClient scope, Type entityType, int skip, int take)
    {
        var queryableMethod = typeof(SqlSugarClient).GetMethods()
            .First(m => m.Name == "Queryable" && m.IsGenericMethod && m.GetParameters().Length == 0);
        var queryable = queryableMethod.MakeGenericMethod(entityType).Invoke(scope, null);
        if (queryable is null)
        {
            return null;
        }

        var skipMethod = queryable.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "Skip" && m.GetParameters().Length == 1);
        var skipped = skipMethod.Invoke(queryable, new object[] { skip });

        var takeMethod = queryable!.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "Take" && m.GetParameters().Length == 1);
        var taken = takeMethod.Invoke(skipped, new object[] { take });

        var toListMethod = taken!.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "ToList" && m.GetParameters().Length == 0);
        return toListMethod.Invoke(taken, null) as System.Collections.IList;
    }

    private static int CountEntity(SqlSugarClient scope, Type entityType)
    {
        var queryableMethod = typeof(SqlSugarClient).GetMethods()
            .First(m => m.Name == "Queryable" && m.IsGenericMethod && m.GetParameters().Length == 0);
        var queryable = queryableMethod.MakeGenericMethod(entityType).Invoke(scope, null);
        if (queryable is null)
        {
            return 0;
        }
        var countMethod = queryable.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "Count" && m.GetParameters().Length == 0);
        return (int)countMethod.Invoke(queryable, null)!;
    }

    private static void InsertBatch(SqlSugarClient scope, Type entityType, System.Collections.IList rows)
    {
        if (rows.Count == 0)
        {
            return;
        }
        var arrayType = entityType.MakeArrayType();
        var typedArray = Array.CreateInstance(entityType, rows.Count);
        for (var i = 0; i < rows.Count; i += 1)
        {
            typedArray.SetValue(rows[i], i);
        }

        var insertableMethod = typeof(SqlSugarClient).GetMethods()
            .First(m => m.Name == "Insertable"
                        && m.IsGenericMethod
                        && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType.IsArray);
        var insertable = insertableMethod.MakeGenericMethod(entityType).Invoke(scope, new object[] { typedArray });
        if (insertable is null)
        {
            return;
        }

        var execMethod = insertable.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "ExecuteCommand" && m.GetParameters().Length == 0);
        execMethod.Invoke(insertable, null);
    }

    private static SqlSugarClient OpenScope(DbConnectionConfig connection)
    {
        var dbType = connection.DbType.Equals("MySql", StringComparison.OrdinalIgnoreCase) ? DbType.MySql
            : connection.DbType.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) ? DbType.PostgreSQL
            : connection.DbType.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) ? DbType.SqlServer
            : DbType.Sqlite;

        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = connection.ConnectionString ?? string.Empty,
            DbType = dbType,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = (property, column) =>
                {
                    if (property.Name == nameof(Atlas.Core.Abstractions.TenantEntity.TenantId)
                        && property.PropertyType == typeof(Atlas.Core.Tenancy.TenantId))
                    {
                        column.IsIgnore = true;
                    }
                }
            }
        });
    }

    private static DbConnectionConfig BuildSourceConfig(DataMigrationJob job)
        => new(job.SourceDbType, job.SourceDbType, "raw", job.SourceConnectionString, null);

    private static DbConnectionConfig BuildTargetConfig(DataMigrationJob job)
        => new(job.TargetDbType, job.TargetDbType, "raw", job.TargetConnectionString, null);

    private static DataMigrationJobDto MapJob(DataMigrationJob job)
    {
        var moduleScope = string.IsNullOrEmpty(job.ModuleScopeJson)
            ? new DataMigrationModuleScopeDto(new[] { "all" }, null)
            : JsonSerializer.Deserialize<DataMigrationModuleScopeDto>(job.ModuleScopeJson)
              ?? new DataMigrationModuleScopeDto(new[] { "all" }, null);

        return new DataMigrationJobDto(
            Id: job.Id.ToString(),
            State: job.State,
            Mode: job.Mode,
            Source: BuildSourceConfig(job),
            Target: BuildTargetConfig(job),
            SourceFingerprint: job.SourceFingerprint,
            TargetFingerprint: job.TargetFingerprint,
            ModuleScope: moduleScope,
            TotalEntities: job.TotalEntities,
            CompletedEntities: job.CompletedEntities,
            FailedEntities: job.FailedEntities,
            TotalRows: job.TotalRows,
            CopiedRows: job.CopiedRows,
            ProgressPercent: job.ProgressPercent,
            CurrentEntityName: job.CurrentEntityName,
            CurrentBatchNo: job.CurrentBatchNo,
            StartedAt: job.StartedAt,
            FinishedAt: job.FinishedAt,
            ErrorSummary: job.ErrorSummary,
            CreatedAt: job.CreatedAt,
            UpdatedAt: job.UpdatedAt);
    }

    private async Task AppendLogAsync(long jobId, string level, string module, string message, string? entityName)
    {
        var log = new DataMigrationLog(
            _tenantProvider.GetTenantId(),
            id: _idGen.NextId(),
            jobId: jobId,
            level: level,
            module: module,
            message: message,
            entityName: entityName,
            now: DateTimeOffset.UtcNow);
        await _db.Insertable(log).ExecuteCommandAsync().ConfigureAwait(false);
    }

    public static string ComputeFingerprint(DbConnectionConfig connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var canonical = $"{connection.DbType}|{connection.ConnectionString ?? string.Empty}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static long ParseJobId(string jobId)
    {
        if (long.TryParse(jobId, out var id))
        {
            return id;
        }
        throw new InvalidOperationException($"invalid jobId {jobId}");
    }
}
