using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Enums;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using Atlas.Infrastructure.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;
using System.Security.Cryptography;
using System.Text;

namespace Atlas.Infrastructure.Services;

public sealed class AppMigrationService : IAppMigrationService
{
    private const string SchemaMigrationScopeApp = "app";
    private const string AppSqlScriptRoot = "sql/app";
    private const string AppSqlScriptLegacyRoot = "sql/app/common";

    private static readonly string[] RequiredAppSchemaTables =
    {
        "DynamicTable",
        "DynamicField",
        "DynamicIndex",
        "DynamicRelation",
        "FieldPermission",
        "MigrationRecord",
        "DynamicSchemaMigration",
        "AppMember",
        "AppRole",
        "AppUserRole",
        "AppRolePermission",
        "AppPermission",
        "AppRolePage",
        "AppDepartment",
        "AppPosition",
        "AppProject",
        "RuntimeRoute",
        "AppDatabaseSchemaVersion"
    };

    private static readonly (string Table, string Column)[] RequiredAppSchemaColumns =
    {
        ("RuntimeRoute", "AppKey"),
        ("RuntimeRoute", "PageKey"),
        ("RuntimeRoute", "SchemaVersion"),
        ("AppMember", "AppId"),
        ("AppRole", "DataScope"),
        ("AppPermission", "Code")
    };

    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;
    private readonly ITenantDbConnectionFactory _tenantDbConnectionFactory;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly AppDatabaseProvisioningService _provisioningService;
    private readonly SqliteDisasterRecoveryOptions _sqliteDisasterRecoveryOptions;
    private readonly ILogger<AppMigrationService> _logger;

    public AppMigrationService(
        ISqlSugarClient mainDb,
        IAppDbScopeFactory appDbScopeFactory,
        ITenantDbConnectionFactory tenantDbConnectionFactory,
        IIdGeneratorAccessor idGeneratorAccessor,
        AppDatabaseProvisioningService provisioningService,
        IOptions<SqliteDisasterRecoveryOptions> sqliteDisasterRecoveryOptions,
        ILogger<AppMigrationService> logger)
    {
        _mainDb = mainDb;
        _appDbScopeFactory = appDbScopeFactory;
        _tenantDbConnectionFactory = tenantDbConnectionFactory;
        _idGeneratorAccessor = idGeneratorAccessor;
        _provisioningService = provisioningService;
        _sqliteDisasterRecoveryOptions = sqliteDisasterRecoveryOptions.Value;
        _logger = logger;
    }

    public async Task<long> CreateTaskAsync(
        TenantId tenantId,
        long userId,
        AppMigrationTaskCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(request.AppInstanceId, out var appInstanceId) || appInstanceId <= 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, $"应用实例ID无效。AppInstanceId={request.AppInstanceId}");
        }

        var dataSourceId = await ResolveDataSourceIdAsync(tenantId, appInstanceId, cancellationToken);
        if (dataSourceId <= 0)
        {
            throw new BusinessException(
                ErrorCodes.AppDataSourceNotBound,
                $"应用实例未绑定可用数据源，无法创建迁移任务。AppInstanceId={appInstanceId}");
        }

        var now = DateTimeOffset.UtcNow;
        var task = new AppMigrationTask(
            tenantId,
            appInstanceId,
            dataSourceId,
            request.ReadOnlyWindow,
            request.EnableDualWrite,
            request.EnableRollback,
            userId,
            _idGeneratorAccessor.NextId(),
            now);
        await _mainDb.Insertable(task).ExecuteCommandAsync(cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
        return task.Id;
    }

    public async Task<PagedResult<AppMigrationTaskListItem>> QueryTasksAsync(
        TenantId tenantId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _mainDb.Queryable<AppMigrationTask>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            query = query.Where(x =>
                x.Status.Contains(request.Keyword)
                || x.Phase.Contains(request.Keyword)
                || x.TenantAppInstanceId.ToString().Contains(request.Keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(request.PageIndex, request.PageSize, cancellationToken);

        var items = rows.Select(x => new AppMigrationTaskListItem(
            x.Id.ToString(),
            x.TenantIdValue.ToString("D"),
            x.TenantAppInstanceId.ToString(),
            x.Status,
            x.Phase,
            x.TotalItems,
            x.CompletedItems,
            x.FailedItems,
            x.ProgressPercent,
            x.CreatedAt,
            x.StartedAt,
            x.FinishedAt,
            x.ErrorSummary,
            x.SchemaRepairLog)).ToArray();
        return new PagedResult<AppMigrationTaskListItem>(items, total, request.PageIndex, request.PageSize);
    }

    public async Task<AppMigrationTaskDetail?> GetTaskAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await FindTaskAsync(tenantId, taskId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        return MapDetail(task);
    }

    public async Task<AppMigrationPrecheckResult> PrecheckAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(tenantId, taskId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        task.MarkPrechecking(task.UpdatedBy <= 0 ? task.CreatedBy : task.UpdatedBy, now);
        await UpdateTaskAsync(task, cancellationToken);

        var checks = new List<string>();
        var warnings = new List<string>();

        var info = await _tenantDbConnectionFactory.GetConnectionInfoAsync(
            tenantId.Value.ToString("D"),
            task.TenantAppInstanceId,
            cancellationToken);
        if (info is null)
        {
            checks.Add("应用绑定数据源不可解析");
            task.MarkFailed(task.UpdatedBy, now, "应用绑定数据源不可解析");
            await UpdateTaskAsync(task, cancellationToken);
            await AddSnapshotAsync(task, cancellationToken);
            return new AppMigrationPrecheckResult(task.Id.ToString(), false, checks, warnings);
        }

        checks.Add("应用绑定数据源可解析");

        var appTables = await _mainDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        checks.Add($"待迁动态表数量：{appTables.Count}");
        if (appTables.Count == 0)
        {
            warnings.Add("该应用暂无动态表元数据，迁移将主要处理应用权限和运行态数据。");
        }

        task.MarkReady(task.UpdatedBy, now);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
        return new AppMigrationPrecheckResult(task.Id.ToString(), true, checks, warnings);
    }

    public async Task<AppMigrationActionResult> StartAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(tenantId, taskId, cancellationToken);
        var precheck = await PrecheckAsync(tenantId, taskId, cancellationToken);
        if (!precheck.CanStart)
        {
            return new AppMigrationActionResult(false, task.Id.ToString(), task.Status, "预检查未通过");
        }

        var now = DateTimeOffset.UtcNow;
        task.MarkRunning(userId, now, 21);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);

        try
        {
            return await RunMigrationPipelineAsync(
                tenantId,
                task,
                userId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            var recoveryResult = await TryAutoRecoverAndRetryAsync(
                tenantId,
                task,
                userId,
                ex,
                cancellationToken);
            if (recoveryResult.Success)
            {
                return new AppMigrationActionResult(true, task.Id.ToString(), task.Status, "迁移完成，等待切换");
            }

            var summary = recoveryResult.Attempted
                ? (recoveryResult.FailureSummary ?? SqliteConstraintErrorFormatter.Format(ex))
                : SqliteConstraintErrorFormatter.Format(ex);
            task.MarkFailed(userId, DateTimeOffset.UtcNow, summary);
            await UpdateTaskAsync(task, cancellationToken);
            await AddSnapshotAsync(task, cancellationToken);
            return new AppMigrationActionResult(false, task.Id.ToString(), task.Status, summary);
        }
    }

    private async Task<AppMigrationActionResult> RunMigrationPipelineAsync(
        TenantId tenantId,
        AppMigrationTask task,
        long userId,
        CancellationToken cancellationToken)
    {
        var appDb = await EnsureSchemaBeforeDataSyncAsync(
            tenantId,
            task,
            userId,
            cancellationToken);

        var completed = 0;
        await CopyDynamicTablesAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyDynamicFieldsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyDynamicIndexesAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyDynamicRelationsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyFieldPermissionsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyMigrationRecordsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyAppMembersAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyAppRolesAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyAppPermissionsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyAppUserRolesAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyAppRolePermissionsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyAppRolePagesAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyAppDepartmentsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyAppPositionsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyAppProjectsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyWorkflowMetasAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyWorkflowDraftsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyWorkflowVersionsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyWorkflowExecutionsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyWorkflowNodeExecutionsAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);
        await CopyRuntimeRoutesAsync(tenantId, task, appDb, userId, ++completed, cancellationToken);

        task.MarkCutoverReady(userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
        return new AppMigrationActionResult(true, task.Id.ToString(), task.Status, "迁移完成，等待切换");
    }

    private async Task<SqliteRecoveryResult> TryAutoRecoverAndRetryAsync(
        TenantId tenantId,
        AppMigrationTask task,
        long userId,
        Exception originalException,
        CancellationToken cancellationToken)
    {
        if (!_sqliteDisasterRecoveryOptions.Enabled || _sqliteDisasterRecoveryOptions.MaxAutoRetryCount < 1)
        {
            return SqliteRecoveryResult.NotAttempted();
        }

        if (SqliteDisasterRecoveryClassifier.Classify(originalException) != SqliteFailureKind.DiskImageMalformed)
        {
            return SqliteRecoveryResult.NotAttempted();
        }

        var tenantIdText = tenantId.Value.ToString("D");
        var connectionInfo = await _tenantDbConnectionFactory.GetConnectionInfoAsync(
            tenantIdText,
            task.TenantAppInstanceId,
            cancellationToken);
        if (connectionInfo is null || !string.Equals(connectionInfo.DbType, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            return SqliteRecoveryResult.NotAttempted();
        }

        if (!TryGetSqliteDatabasePath(connectionInfo.ConnectionString, out var dbPath))
        {
            return SqliteRecoveryResult.NotAttempted();
        }

        _logger.LogWarning(
            originalException,
            "检测到应用库 SQLite 损坏，开始自动容灾重建。tenantId={TenantId} appInstanceId={AppInstanceId} taskId={TaskId} recoveryAttempt={RecoveryAttempt}",
            tenantIdText,
            task.TenantAppInstanceId,
            task.Id,
            1);

        await AppendSchemaRepairLogAsync(
            task,
            $"DisasterRecovery: corruption-detected at {DateTimeOffset.UtcNow:O}",
            userId,
            cancellationToken);

        try
        {
            _tenantDbConnectionFactory.InvalidateCache(tenantIdText, task.TenantAppInstanceId);
            _appDbScopeFactory.InvalidateAppClientCache(tenantId, task.TenantAppInstanceId);
            SqliteConnection.ClearAllPools();

            QuarantineCorruptedSqliteFiles(dbPath);
            await AppendSchemaRepairLogAsync(
                task,
                $"DisasterRecovery: quarantine completed, path={dbPath}",
                userId,
                cancellationToken);

            await _provisioningService.EnsureSchemaAsync(tenantId, task.TenantAppInstanceId, cancellationToken);
            _appDbScopeFactory.InvalidateAppClientCache(tenantId, task.TenantAppInstanceId);
            await AppendSchemaRepairLogAsync(
                task,
                "DisasterRecovery: empty schema recreated",
                userId,
                cancellationToken);

            await RunMigrationPipelineAsync(tenantId, task, userId, cancellationToken);
            await AppendSchemaRepairLogAsync(
                task,
                "DisasterRecovery: rerun succeeded",
                userId,
                cancellationToken);

            return SqliteRecoveryResult.Succeeded();
        }
        catch (Exception recoveryException)
        {
            _logger.LogError(
                recoveryException,
                "应用库自动容灾重建失败。tenantId={TenantId} appInstanceId={AppInstanceId} taskId={TaskId}",
                tenantIdText,
                task.TenantAppInstanceId,
                task.Id);

            var summary = SqliteConstraintErrorFormatter.Format(recoveryException);
            await AppendSchemaRepairLogAsync(
                task,
                $"DisasterRecovery: rerun failed, summary={summary}",
                userId,
                cancellationToken);
            return SqliteRecoveryResult.Failed(summary);
        }
    }

    private async Task<ISqlSugarClient> EnsureSchemaBeforeDataSyncAsync(
        TenantId tenantId,
        AppMigrationTask task,
        long userId,
        CancellationToken cancellationToken)
    {
        task.MarkObjectProgress("SchemaInit", 0, 0, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);

        await _provisioningService.EnsureSchemaAsync(tenantId, task.TenantAppInstanceId, cancellationToken);
        var appDb = await _appDbScopeFactory.GetAppClientAsync(tenantId, task.TenantAppInstanceId, cancellationToken);
        await ExecutePendingSqlScriptsAsync(tenantId, task, appDb, userId, cancellationToken);
        EnsureRequiredTablesReady(appDb);
        EnsureRequiredColumnsReady(appDb);

        if (SqliteSchemaAlignment.IsSqlite(appDb))
        {
            var report = await SqliteSchemaAlignment.EnsureAppMembershipDomainSchemaAsync(appDb, cancellationToken);
            var logText = string.Join(" | ", report.Messages);
            task.SetSchemaRepairLog(MergeSchemaRepairLog(task.SchemaRepairLog, logText), userId, DateTimeOffset.UtcNow);
            await UpdateTaskAsync(task, cancellationToken);
            await AddSnapshotAsync(task, cancellationToken);
        }

        task.MarkObjectProgress("SchemaReady", 0, 0, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
        return appDb;
    }

    private async Task ExecutePendingSqlScriptsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaMigrationsTableAsync(appDb, cancellationToken);

        var scriptFiles = DiscoverAppSqlScripts();
        if (scriptFiles.Count == 0)
        {
            await AppendSchemaRepairLogAsync(task, "SqlMigration: no app sql scripts found, skip.", userId, cancellationToken);
            return;
        }

        var targetKey = $"{tenantId.Value:D}:{task.TenantAppInstanceId}";
        var appliedScriptNames = await appDb.Queryable<SchemaMigrationEntry>()
            .Where(item => item.Scope == SchemaMigrationScopeApp && item.TargetKey == targetKey)
            .Select(item => item.ScriptName)
            .ToListAsync(cancellationToken);
        var appliedSet = appliedScriptNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var script in scriptFiles)
        {
            if (appliedSet.Contains(script.FileName))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(script.FullPath, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            var checksum = ComputeSha256(content);
            await appDb.Ado.ExecuteCommandAsync(content);

            var record = new SchemaMigrationEntry
            {
                Scope = SchemaMigrationScopeApp,
                TargetKey = targetKey,
                ScriptName = script.FileName,
                ChecksumSha256 = checksum,
                ExecutedAt = DateTimeOffset.UtcNow,
                ExecutedBy = userId > 0 ? userId.ToString() : "system"
            };
            await appDb.Insertable(record).ExecuteCommandAsync(cancellationToken);
            await AppendSchemaRepairLogAsync(
                task,
                $"SqlMigration: applied {script.FileName} (sha256={checksum})",
                userId,
                cancellationToken);
        }
    }

    private static void EnsureRequiredTablesReady(ISqlSugarClient appDb)
    {
        var tableNames = appDb.DbMaintenance.GetTableInfoList(false)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = RequiredAppSchemaTables
            .Where(table => !tableNames.Contains(table))
            .ToArray();
        if (missing.Length == 0)
        {
            return;
        }

        throw new BusinessException(
            ErrorCodes.ServerError,
            $"应用库结构初始化不完整，缺少表：{string.Join(", ", missing)}");
    }

    private static void EnsureRequiredColumnsReady(ISqlSugarClient appDb)
    {
        foreach (var group in RequiredAppSchemaColumns.GroupBy(item => item.Table, StringComparer.OrdinalIgnoreCase))
        {
            if (!appDb.DbMaintenance.IsAnyTable(group.Key, false))
            {
                continue;
            }

            var existingColumns = appDb.DbMaintenance.GetColumnInfosByTableName(group.Key, false)
                .Select(item => item.DbColumnName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingColumns = group
                .Select(item => item.Column)
                .Where(column => !existingColumns.Contains(column))
                .ToArray();
            if (missingColumns.Length > 0)
            {
                throw new BusinessException(
                    ErrorCodes.ServerError,
                    $"应用库结构校验失败，表 {group.Key} 缺少列：{string.Join(", ", missingColumns)}");
            }
        }
    }

    private static IReadOnlyList<SqlScriptDescriptor> DiscoverAppSqlScripts()
    {
        var repoRoot = ResolveRepoRoot();
        var scriptRoots = new[]
        {
            Path.Combine(repoRoot, AppSqlScriptRoot),
            Path.Combine(repoRoot, AppSqlScriptLegacyRoot)
        };
        var list = new List<SqlScriptDescriptor>();
        foreach (var root in scriptRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var files = Directory.GetFiles(root, "*.sql", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => new SqlScriptDescriptor(Path.GetFileName(path), path));
            list.AddRange(files);
        }

        return list;
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var backendPath = Path.Combine(directory.FullName, "src", "backend");
            if (Directory.Exists(backendPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static Task EnsureSchemaMigrationsTableAsync(
        ISqlSugarClient appDb,
        CancellationToken cancellationToken)
    {
        if (!appDb.DbMaintenance.IsAnyTable("SchemaMigrations", false))
        {
            appDb.CodeFirst.InitTables<SchemaMigrationEntry>();
            return Task.CompletedTask;
        }

        _ = cancellationToken;
        return Task.CompletedTask;
    }

    private async Task AppendSchemaRepairLogAsync(
        AppMigrationTask task,
        string message,
        long userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        task.SetSchemaRepairLog(MergeSchemaRepairLog(task.SchemaRepairLog, message), userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private static string MergeSchemaRepairLog(string? original, string appended)
    {
        if (string.IsNullOrWhiteSpace(original))
        {
            return appended;
        }

        return $"{original} | {appended}";
    }

    private static bool TryGetSqliteDatabasePath(string connectionString, out string databasePath)
    {
        databasePath = string.Empty;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (!part.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
                && !part.StartsWith("DataSource=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = part.Split('=', 2)[1].Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            databasePath = Path.GetFullPath(value);
            return true;
        }

        return false;
    }

    private void QuarantineCorruptedSqliteFiles(string databasePath)
    {
        var targetBaseDirectory = _sqliteDisasterRecoveryOptions.KeepCorruptedFiles
            ? Path.GetFullPath(_sqliteDisasterRecoveryOptions.QuarantineDirectory)
            : Path.GetDirectoryName(databasePath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(targetBaseDirectory);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        MoveOrDeleteSqliteArtifact(databasePath, targetBaseDirectory, timestamp);
        MoveOrDeleteSqliteArtifact($"{databasePath}-wal", targetBaseDirectory, timestamp);
        MoveOrDeleteSqliteArtifact($"{databasePath}-shm", targetBaseDirectory, timestamp);
    }

    private void MoveOrDeleteSqliteArtifact(string sourcePath, string targetDirectory, string timestamp)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        if (!_sqliteDisasterRecoveryOptions.KeepCorruptedFiles)
        {
            File.Delete(sourcePath);
            return;
        }

        var fileName = Path.GetFileName(sourcePath);
        var targetPath = Path.Combine(targetDirectory, $"{fileName}.corrupt.{timestamp}");
        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        File.Move(sourcePath, targetPath);
    }

    public async Task<AppMigrationTaskProgress?> GetProgressAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await FindTaskAsync(tenantId, taskId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        return new AppMigrationTaskProgress(
            task.Id.ToString(),
            task.Status,
            task.Phase,
            task.TotalItems,
            task.CompletedItems,
            task.FailedItems,
            task.ProgressPercent,
            task.CurrentObjectName,
            task.CurrentBatchNo,
            task.UpdatedAt,
            task.ErrorSummary,
            task.SchemaRepairLog);
    }

    public async Task<AppIntegrityCheckSummary> ValidateIntegrityAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(tenantId, taskId, cancellationToken);
        task.MarkValidating(userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);

        var appDb = await _appDbScopeFactory.GetAppClientAsync(tenantId, task.TenantAppInstanceId, cancellationToken);
        var checks = new List<AppIntegrityCheckItem>();

        var mainDynamicTableCount = await _mainDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .CountAsync(cancellationToken);
        var appDynamicTableCount = await appDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .CountAsync(cancellationToken);
        checks.Add(new AppIntegrityCheckItem(
            tenantId,
            0,
            "TableCount",
            nameof(DynamicTable),
            mainDynamicTableCount == appDynamicTableCount,
            $"main={mainDynamicTableCount}, app={appDynamicTableCount}",
            _idGeneratorAccessor.NextId(),
            DateTimeOffset.UtcNow));

        var mainRoleCount = await _mainDb.Queryable<AppRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .CountAsync(cancellationToken);
        var appRoleCount = await appDb.Queryable<AppRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .CountAsync(cancellationToken);
        checks.Add(new AppIntegrityCheckItem(
            tenantId,
            0,
            "RowCount",
            nameof(AppRole),
            mainRoleCount == appRoleCount,
            $"main={mainRoleCount}, app={appRoleCount}",
            _idGeneratorAccessor.NextId(),
            DateTimeOffset.UtcNow));

        var executionRows = await _mainDb.Queryable<WorkflowExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .Select(x => new { x.Id, x.WorkflowId })
            .ToListAsync(cancellationToken);
        var workflowIdList = executionRows.Select(x => x.WorkflowId).Distinct().ToArray();
        var executionIdList = executionRows.Select(x => x.Id).Distinct().ToArray();

        var mainWorkflowMetaCount = workflowIdList.Length == 0
            ? 0
            : await _mainDb.Queryable<WorkflowMeta>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(workflowIdList, x.Id))
                .CountAsync(cancellationToken);
        var appWorkflowMetaCount = workflowIdList.Length == 0
            ? 0
            : await appDb.Queryable<WorkflowMeta>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(workflowIdList, x.Id))
                .CountAsync(cancellationToken);
        checks.Add(new AppIntegrityCheckItem(
            tenantId,
            0,
            "RowCount",
            nameof(WorkflowMeta),
            mainWorkflowMetaCount == appWorkflowMetaCount,
            $"main={mainWorkflowMetaCount}, app={appWorkflowMetaCount} (workflowIds={workflowIdList.Length})",
            _idGeneratorAccessor.NextId(),
            DateTimeOffset.UtcNow));

        var mainWorkflowDraftCount = workflowIdList.Length == 0
            ? 0
            : await _mainDb.Queryable<WorkflowDraft>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(workflowIdList, x.WorkflowId))
                .CountAsync(cancellationToken);
        var appWorkflowDraftCount = workflowIdList.Length == 0
            ? 0
            : await appDb.Queryable<WorkflowDraft>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(workflowIdList, x.WorkflowId))
                .CountAsync(cancellationToken);
        checks.Add(new AppIntegrityCheckItem(
            tenantId,
            0,
            "RowCount",
            nameof(WorkflowDraft),
            mainWorkflowDraftCount == appWorkflowDraftCount,
            $"main={mainWorkflowDraftCount}, app={appWorkflowDraftCount}",
            _idGeneratorAccessor.NextId(),
            DateTimeOffset.UtcNow));

        var mainWorkflowVersionCount = workflowIdList.Length == 0
            ? 0
            : await _mainDb.Queryable<WorkflowVersion>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(workflowIdList, x.WorkflowId))
                .CountAsync(cancellationToken);
        var appWorkflowVersionCount = workflowIdList.Length == 0
            ? 0
            : await appDb.Queryable<WorkflowVersion>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(workflowIdList, x.WorkflowId))
                .CountAsync(cancellationToken);
        checks.Add(new AppIntegrityCheckItem(
            tenantId,
            0,
            "RowCount",
            nameof(WorkflowVersion),
            mainWorkflowVersionCount == appWorkflowVersionCount,
            $"main={mainWorkflowVersionCount}, app={appWorkflowVersionCount}",
            _idGeneratorAccessor.NextId(),
            DateTimeOffset.UtcNow));

        var mainWorkflowExecutionCount = await _mainDb.Queryable<WorkflowExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .CountAsync(cancellationToken);
        var appWorkflowExecutionCount = await appDb.Queryable<WorkflowExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .CountAsync(cancellationToken);
        checks.Add(new AppIntegrityCheckItem(
            tenantId,
            0,
            "RowCount",
            nameof(WorkflowExecution),
            mainWorkflowExecutionCount == appWorkflowExecutionCount,
            $"main={mainWorkflowExecutionCount}, app={appWorkflowExecutionCount}",
            _idGeneratorAccessor.NextId(),
            DateTimeOffset.UtcNow));

        var mainNodeCount = executionIdList.Length == 0
            ? 0
            : await _mainDb.Queryable<WorkflowNodeExecution>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(executionIdList, x.ExecutionId))
                .CountAsync(cancellationToken);
        var appNodeCount = executionIdList.Length == 0
            ? 0
            : await appDb.Queryable<WorkflowNodeExecution>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(executionIdList, x.ExecutionId))
                .CountAsync(cancellationToken);
        checks.Add(new AppIntegrityCheckItem(
            tenantId,
            0,
            "RowCount",
            nameof(WorkflowNodeExecution),
            mainNodeCount == appNodeCount,
            $"main={mainNodeCount}, app={appNodeCount}",
            _idGeneratorAccessor.NextId(),
            DateTimeOffset.UtcNow));

        var passed = checks.Count(x => x.Status == "Passed");
        var failed = checks.Count - passed;
        var report = new AppDataIntegrityReport(
            tenantId,
            task.Id,
            failed == 0,
            checks.Count,
            passed,
            failed,
            userId,
            _idGeneratorAccessor.NextId(),
            DateTimeOffset.UtcNow);
        await _mainDb.Insertable(report).ExecuteCommandAsync(cancellationToken);

        var reportItems = checks.Select(item => new AppIntegrityCheckItem(
            tenantId,
            report.Id,
            item.CheckType,
            item.ObjectName,
            item.Status == "Passed",
            item.DetailMessage,
            _idGeneratorAccessor.NextId(),
            DateTimeOffset.UtcNow)).ToArray();
        await _mainDb.Insertable(reportItems).ExecuteCommandAsync(cancellationToken);

        if (failed == 0)
        {
            task.MarkCutoverReady(userId, DateTimeOffset.UtcNow);
        }
        else
        {
            task.MarkFailed(userId, DateTimeOffset.UtcNow, "完整性校验未通过");
        }

        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
        return new AppIntegrityCheckSummary(task.Id.ToString(), failed == 0, checks.Count, passed, failed, DateTimeOffset.UtcNow);
    }

    public async Task<AppMigrationActionResult> CutoverAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        AppMigrationCutoverRequest request,
        CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(tenantId, taskId, cancellationToken);
        if (!string.Equals(task.Status, AppMigrationTaskStatuses.CutoverReady, StringComparison.Ordinal))
        {
            return new AppMigrationActionResult(false, task.Id.ToString(), task.Status, "任务尚未达到可切换状态");
        }

        var policy = await _mainDb.Queryable<AppDataRoutePolicy>()
            .FirstAsync(x => x.TenantIdValue == tenantId.Value && x.AppInstanceId == task.TenantAppInstanceId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (policy is null)
        {
            policy = new AppDataRoutePolicy(
                tenantId,
                task.TenantAppInstanceId,
                "AppOnly",
                request.EnableReadOnlyWindow,
                request.EnableDualWrite,
                userId,
                _idGeneratorAccessor.NextId(),
                now);
            await _mainDb.Insertable(policy).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            policy.SetMode("AppOnly", request.EnableReadOnlyWindow, request.EnableDualWrite, userId, now);
            await _mainDb.Updateable(policy).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkCutoverCompleted(userId, now, request.EnableReadOnlyWindow, request.EnableDualWrite);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
        _tenantDbConnectionFactory.InvalidateCache(tenantId.Value.ToString("D"), task.TenantAppInstanceId);
        return new AppMigrationActionResult(true, task.Id.ToString(), task.Status, "切换完成");
    }

    public async Task<AppMigrationActionResult> RollbackCutoverAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(tenantId, taskId, cancellationToken);
        var policy = await _mainDb.Queryable<AppDataRoutePolicy>()
            .FirstAsync(x => x.TenantIdValue == tenantId.Value && x.AppInstanceId == task.TenantAppInstanceId, cancellationToken);
        if (policy is null)
        {
            return new AppMigrationActionResult(false, task.Id.ToString(), task.Status, "未找到切换策略，无法回切");
        }

        policy.SetMode("MainOnly", true, false, userId, DateTimeOffset.UtcNow);
        await _mainDb.Updateable(policy).ExecuteCommandAsync(cancellationToken);

        task.MarkRolledBack(userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
        _tenantDbConnectionFactory.InvalidateCache(tenantId.Value.ToString("D"), task.TenantAppInstanceId);
        return new AppMigrationActionResult(true, task.Id.ToString(), task.Status, "已回切到主库");
    }

    public async Task<AppMigrationActionResult> ResetFailedTaskAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(tenantId, taskId, cancellationToken);
        if (!string.Equals(task.Status, AppMigrationTaskStatuses.Failed, StringComparison.Ordinal))
        {
            return new AppMigrationActionResult(false, task.Id.ToString(), task.Status, "仅失败状态任务允许重置。");
        }

        task.ResetForRetry(userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
        return new AppMigrationActionResult(true, task.Id.ToString(), task.Status, "任务已重置，可重新执行迁移。");
    }

    public async Task<AppMigrationActionResult> RecoverCorruptedTaskAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(tenantId, taskId, cancellationToken);
        if (!string.Equals(task.Status, AppMigrationTaskStatuses.Failed, StringComparison.Ordinal))
        {
            return new AppMigrationActionResult(false, task.Id.ToString(), task.Status, "仅失败状态任务允许执行恢复。");
        }

        task.ResetForRetry(userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
        return await StartAsync(tenantId, userId, taskId, cancellationToken);
    }

    public async Task<AppMigrationBindingRepairResult> RepairPrimaryBindingAsync(
        TenantId tenantId,
        long userId,
        AppMigrationBindingRepairRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(request.AppInstanceId, out var appInstanceId) || appInstanceId <= 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, $"应用实例ID无效。AppInstanceId={request.AppInstanceId}");
        }

        var dataSourceId = await ResolveRepairCandidateDataSourceIdAsync(tenantId, appInstanceId, cancellationToken);
        if (dataSourceId <= 0)
        {
            throw new BusinessException(
                ErrorCodes.ValidationError,
                $"未找到可用于修复的应用主数据源。AppInstanceId={appInstanceId}");
        }

        var existingPrimary = await _mainDb.Queryable<TenantAppDataSourceBinding>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.TenantAppInstanceId == appInstanceId
                && x.BindingType == TenantAppDataSourceBindingType.Primary)
            .FirstAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (existingPrimary is null)
        {
            var binding = new TenantAppDataSourceBinding(
                tenantId,
                appInstanceId,
                dataSourceId,
                TenantAppDataSourceBindingType.Primary,
                userId,
                _idGeneratorAccessor.NextId(),
                now,
                "迁移任务显式修复主数据源绑定");
            await _mainDb.Insertable(binding).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            existingPrimary.Rebind(
                dataSourceId,
                TenantAppDataSourceBindingType.Primary,
                userId,
                now,
                "迁移任务显式修复主数据源绑定");
            await _mainDb.Updateable(existingPrimary)
                .Where(x => x.Id == existingPrimary.Id && x.TenantIdValue == tenantId.Value)
                .ExecuteCommandAsync(cancellationToken);
        }

        _tenantDbConnectionFactory.InvalidateCache(tenantId.Value.ToString("D"), appInstanceId);
        return new AppMigrationBindingRepairResult(
            appInstanceId.ToString(),
            dataSourceId.ToString(),
            true,
            "主数据源绑定修复成功。");
    }

    private async Task<long> ResolveDataSourceIdAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken)
    {
        var binding = await _mainDb.Queryable<TenantAppDataSourceBinding>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.TenantAppInstanceId == appInstanceId
                && x.IsActive
                && x.BindingType == TenantAppDataSourceBindingType.Primary
                && x.DataSourceId > 0)
            .OrderByDescending(x => x.UpdatedAt ?? x.BoundAt)
            .FirstAsync(cancellationToken);
        if (binding is null || binding.DataSourceId <= 0)
        {
            return 0;
        }

        var tenantIdText = tenantId.Value.ToString("D");
        var targetDataSource = await _mainDb.Queryable<TenantDataSource>()
            .Where(x =>
                x.TenantIdValue == tenantIdText
                && x.Id == binding.DataSourceId
                && x.IsActive)
            .Select(x => x.Id)
            .FirstAsync(cancellationToken);
        return targetDataSource;
    }

    private async Task<long> ResolveRepairCandidateDataSourceIdAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken)
    {
        var appDataSourceId = await _mainDb.Queryable<LowCodeApp>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == appInstanceId)
            .Select(x => x.DataSourceId)
            .FirstAsync(cancellationToken);
        if (appDataSourceId.HasValue && appDataSourceId.Value > 0)
        {
            return await EnsureDataSourceActiveAsync(tenantId, appDataSourceId.Value, cancellationToken)
                ? appDataSourceId.Value
                : 0;
        }

        var tenantAppDataSourceId = await _mainDb.Queryable<TenantApplication>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppInstanceId == appInstanceId)
            .Select(x => x.DataSourceId)
            .FirstAsync(cancellationToken);
        if (tenantAppDataSourceId.HasValue && tenantAppDataSourceId.Value > 0)
        {
            return await EnsureDataSourceActiveAsync(tenantId, tenantAppDataSourceId.Value, cancellationToken)
                ? tenantAppDataSourceId.Value
                : 0;
        }

        var appKey = await _mainDb.Queryable<LowCodeApp>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == appInstanceId)
            .Select(x => x.AppKey)
            .FirstAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(appKey))
        {
            return 0;
        }

        var manifestDataSourceId = await _mainDb.Queryable<AppManifest>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppKey == appKey)
            .Select(x => x.DataSourceId)
            .FirstAsync(cancellationToken);
        if (!manifestDataSourceId.HasValue || manifestDataSourceId.Value <= 0)
        {
            return 0;
        }

        return await EnsureDataSourceActiveAsync(tenantId, manifestDataSourceId.Value, cancellationToken)
            ? manifestDataSourceId.Value
            : 0;
    }

    private async Task<bool> EnsureDataSourceActiveAsync(
        TenantId tenantId,
        long dataSourceId,
        CancellationToken cancellationToken)
    {
        var tenantIdText = tenantId.Value.ToString("D");
        return await _mainDb.Queryable<TenantDataSource>()
            .AnyAsync(
                x => x.TenantIdValue == tenantIdText && x.Id == dataSourceId && x.IsActive,
                cancellationToken);
    }

    private async Task<AppMigrationTask?> FindTaskAsync(TenantId tenantId, long taskId, CancellationToken cancellationToken)
    {
        return await _mainDb.Queryable<AppMigrationTask>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == taskId)
            .FirstAsync(cancellationToken);
    }

    private async Task<AppMigrationTask> RequireTaskAsync(TenantId tenantId, long taskId, CancellationToken cancellationToken)
    {
        var task = await FindTaskAsync(tenantId, taskId, cancellationToken);
        if (task is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "迁移任务不存在。");
        }

        return task;
    }

    private Task UpdateTaskAsync(AppMigrationTask task, CancellationToken cancellationToken)
    {
        return _mainDb.Updateable(task)
            .Where(x => x.Id == task.Id && x.TenantIdValue == task.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    private Task AddSnapshotAsync(AppMigrationTask task, CancellationToken cancellationToken)
    {
        var snapshot = new AppMigrationProgressSnapshot(
            new TenantId(task.TenantIdValue),
            task.Id,
            task.Status,
            task.Phase,
            task.TotalItems,
            task.CompletedItems,
            task.FailedItems,
            task.ProgressPercent,
            task.CurrentObjectName,
            task.CurrentBatchNo,
            task.ErrorSummary,
            task.SchemaRepairLog,
            _idGeneratorAccessor.NextId(),
            DateTimeOffset.UtcNow);
        return _mainDb.Insertable(snapshot).ExecuteCommandAsync(cancellationToken);
    }

    private static AppMigrationTaskDetail MapDetail(AppMigrationTask task)
    {
        return new AppMigrationTaskDetail(
            task.Id.ToString(),
            task.TenantIdValue.ToString("D"),
            task.TenantAppInstanceId.ToString(),
            task.DataSourceId.ToString(),
            task.Status,
            task.Phase,
            task.TotalItems,
            task.CompletedItems,
            task.FailedItems,
            task.ProgressPercent,
            task.CurrentObjectName,
            task.CurrentBatchNo,
            task.ReadOnlyWindow,
            task.EnableDualWrite,
            task.EnableRollback,
            task.CreatedAt,
            task.StartedAt,
            task.FinishedAt,
            task.ErrorSummary,
            task.SchemaRepairLog);
    }

    private async Task CopyDynamicTablesAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<DynamicTable>().Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId).ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(DynamicTable), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyDynamicFieldsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var tableIds = await _mainDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (tableIds.Count == 0)
        {
            task.MarkObjectProgress(nameof(DynamicField), 1, completed, 0, userId, DateTimeOffset.UtcNow);
            await UpdateTaskAsync(task, cancellationToken);
            await AddSnapshotAsync(task, cancellationToken);
            return;
        }

        var rows = await _mainDb.Queryable<DynamicField>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(tableIds, x.TableId))
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<DynamicField>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(tableIds, x.TableId))
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(DynamicField), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyDynamicIndexesAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var tableIds = await _mainDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var rows = tableIds.Count == 0
            ? new List<DynamicIndex>()
            : await _mainDb.Queryable<DynamicIndex>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(tableIds, x.TableId))
                .ToListAsync(cancellationToken);
        if (tableIds.Count > 0)
        {
            await appDb.Deleteable<DynamicIndex>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(tableIds, x.TableId))
                .ExecuteCommandAsync(cancellationToken);
        }
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(DynamicIndex), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyDynamicRelationsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var tableIds = await _mainDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var tableKeys = await _mainDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .Select(x => x.TableKey)
            .ToListAsync(cancellationToken);
        var rows = tableIds.Count == 0
            ? new List<DynamicRelation>()
            : await _mainDb.Queryable<DynamicRelation>()
                .Where(x => x.TenantIdValue == tenantId.Value
                    && (SqlFunc.ContainsArray(tableIds, x.TableId) || SqlFunc.ContainsArray(tableKeys, x.RelatedTableKey)))
                .ToListAsync(cancellationToken);
        if (tableIds.Count > 0 || tableKeys.Count > 0)
        {
            await appDb.Deleteable<DynamicRelation>()
                .Where(x => x.TenantIdValue == tenantId.Value
                    && (SqlFunc.ContainsArray(tableIds, x.TableId) || SqlFunc.ContainsArray(tableKeys, x.RelatedTableKey)))
                .ExecuteCommandAsync(cancellationToken);
        }
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(DynamicRelation), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyFieldPermissionsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var prefix = $"app:{task.TenantAppInstanceId}:";
        var rows = await _mainDb.Queryable<FieldPermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableKey.StartsWith(prefix))
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<FieldPermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableKey.StartsWith(prefix))
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(FieldPermission), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyMigrationRecordsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var tableKeys = await _mainDb.Queryable<DynamicTable>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .Select(x => x.TableKey)
            .ToListAsync(cancellationToken);
        var rows = tableKeys.Count == 0
            ? new List<MigrationRecord>()
            : await _mainDb.Queryable<MigrationRecord>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(tableKeys, x.TableKey))
                .ToListAsync(cancellationToken);
        if (tableKeys.Count > 0)
        {
            await appDb.Deleteable<MigrationRecord>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(tableKeys, x.TableKey))
                .ExecuteCommandAsync(cancellationToken);
        }
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(MigrationRecord), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppMembersAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppMember>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppMember>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            var insertRows = rows
                .Select(row => new AppMember(
                    tenantId,
                    row.AppId,
                    row.UserId,
                    row.JoinedBy,
                    row.JoinedAt,
                    row.Id))
                .ToList();
            await appDb.Insertable(insertRows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppMember), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppRolesAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            var insertRows = rows
                .Select(row =>
                {
                    var role = new AppRole(
                        tenantId,
                        row.AppId,
                        row.Code,
                        row.Name,
                        row.Description,
                        row.IsSystem,
                        row.CreatedBy,
                        row.CreatedAt,
                        row.Id);
                    role.Update(row.Name, row.Description, row.UpdatedBy, row.UpdatedAt);
                    var deptIdsForScope = row.DataScope == DataScopeType.CustomDept
                        ? (string.IsNullOrWhiteSpace(row.DeptIds) ? string.Empty : row.DeptIds)
                        : null;
                    role.SetDataScope(row.DataScope, deptIdsForScope);
                    return role;
                })
                .ToList();
            await appDb.Insertable(insertRows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppRole), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppPermissionsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppPermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppPermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            var insertRows = rows
                .Select(row =>
                {
                    var permission = new AppPermission(
                        tenantId,
                        row.AppId,
                        row.Name,
                        row.Code,
                        row.Type,
                        row.Id);
                    permission.Update(row.Name, row.Type, row.Description);
                    return permission;
                })
                .ToList();
            await appDb.Insertable(insertRows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppPermission), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppUserRolesAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppUserRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppUserRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            var insertRows = rows
                .Select(row => new AppUserRole(
                    tenantId,
                    row.AppId,
                    row.UserId,
                    row.RoleId,
                    row.Id))
                .ToList();
            await appDb.Insertable(insertRows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppUserRole), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppRolePermissionsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppRolePermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppRolePermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            var insertRows = rows
                .Select(row => new AppRolePermission(
                    tenantId,
                    row.AppId,
                    row.RoleId,
                    row.PermissionCode,
                    row.Id))
                .ToList();
            await appDb.Insertable(insertRows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppRolePermission), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppRolePagesAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppRolePage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppRolePage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            var insertRows = rows
                .Select(row => new AppRolePage(
                    tenantId,
                    row.AppId,
                    row.RoleId,
                    row.PageId,
                    row.Id))
                .ToList();
            await appDb.Insertable(insertRows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppRolePage), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppDepartmentsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            var insertRows = rows
                .Select(row =>
                {
                    // 历史库可能将 ParentId 建成 NOT NULL；逻辑根节点用自引用表示（与 AppOrgServices 一致）。
                    var parentId = row.ParentId ?? row.Id;
                    return new AppDepartment(
                        tenantId,
                        row.AppId,
                        row.Name,
                        row.Code,
                        parentId,
                        row.SortOrder,
                        row.Id);
                })
                .ToList();
            await appDb.Insertable(insertRows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppDepartment), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppPositionsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            var insertRows = rows
                .Select(row =>
                {
                    var position = new AppPosition(
                        tenantId,
                        row.AppId,
                        row.Name,
                        row.Code,
                        row.Id);
                    position.Update(row.Name, row.Description, row.IsActive, row.SortOrder);
                    return position;
                })
                .ToList();
            await appDb.Insertable(insertRows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppPosition), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyAppProjectsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<AppProject>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<AppProject>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            var insertRows = rows
                .Select(row =>
                {
                    var project = new AppProject(
                        tenantId,
                        row.AppId,
                        row.Code,
                        row.Name,
                        row.Id);
                    project.Update(row.Name, row.Description, row.IsActive);
                    return project;
                })
                .ToList();
            await appDb.Insertable(insertRows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(AppProject), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    /// <summary>
    /// 应用库内按租户清空 V2 工作流相关表，避免残留与删除顺序问题；随后由后续步骤自主库写入。
    /// </summary>
    private async Task PurgeTenantDagWorkflowInAppAsync(
        TenantId tenantId,
        ISqlSugarClient appDb,
        CancellationToken cancellationToken)
    {
        var executionIds = await appDb.Queryable<WorkflowExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (executionIds.Count > 0)
        {
            await appDb.Deleteable<WorkflowNodeExecution>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(executionIds, x.ExecutionId))
                .ExecuteCommandAsync(cancellationToken);
        }

        await appDb.Deleteable<WorkflowExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
        await appDb.Deleteable<WorkflowVersion>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
        await appDb.Deleteable<WorkflowDraft>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
        await appDb.Deleteable<WorkflowMeta>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task CopyWorkflowMetasAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        await PurgeTenantDagWorkflowInAppAsync(tenantId, appDb, cancellationToken);

        var workflowIds = await _mainDb.Queryable<WorkflowExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .Select(x => x.WorkflowId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (workflowIds.Count > 0)
        {
            var rows = await _mainDb.Queryable<WorkflowMeta>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(workflowIds, x.Id))
                .ToListAsync(cancellationToken);
            if (rows.Count > 0)
            {
                await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
            }
        }

        task.MarkObjectProgress(nameof(WorkflowMeta), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyWorkflowDraftsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var workflowIds = await _mainDb.Queryable<WorkflowExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .Select(x => x.WorkflowId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (workflowIds.Count > 0)
        {
            var rows = await _mainDb.Queryable<WorkflowDraft>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(workflowIds, x.WorkflowId))
                .ToListAsync(cancellationToken);
            if (rows.Count > 0)
            {
                await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
            }
        }

        task.MarkObjectProgress(nameof(WorkflowDraft), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyWorkflowVersionsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var workflowIds = await _mainDb.Queryable<WorkflowExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .Select(x => x.WorkflowId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (workflowIds.Count > 0)
        {
            var rows = await _mainDb.Queryable<WorkflowVersion>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(workflowIds, x.WorkflowId))
                .ToListAsync(cancellationToken);
            if (rows.Count > 0)
            {
                await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
            }
        }

        task.MarkObjectProgress(nameof(WorkflowVersion), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyWorkflowExecutionsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var rows = await _mainDb.Queryable<WorkflowExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .ToListAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(WorkflowExecution), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyWorkflowNodeExecutionsAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var executionIds = await _mainDb.Queryable<WorkflowExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == task.TenantAppInstanceId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (executionIds.Count > 0)
        {
            var rows = await _mainDb.Queryable<WorkflowNodeExecution>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(executionIds, x.ExecutionId))
                .ToListAsync(cancellationToken);
            if (rows.Count > 0)
            {
                await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
            }
        }

        task.MarkObjectProgress(nameof(WorkflowNodeExecution), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private async Task CopyRuntimeRoutesAsync(
        TenantId tenantId,
        AppMigrationTask task,
        ISqlSugarClient appDb,
        long userId,
        int completed,
        CancellationToken cancellationToken)
    {
        var app = await _mainDb.Queryable<LowCodeApp>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == task.TenantAppInstanceId)
            .FirstAsync(cancellationToken);
        if (app is null)
        {
            task.MarkObjectProgress(nameof(RuntimeRoute), 1, completed, 0, userId, DateTimeOffset.UtcNow);
            await UpdateTaskAsync(task, cancellationToken);
            await AddSnapshotAsync(task, cancellationToken);
            return;
        }

        var rows = await _mainDb.Queryable<RuntimeRoute>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppKey == app.AppKey)
            .ToListAsync(cancellationToken);
        await appDb.Deleteable<RuntimeRoute>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppKey == app.AppKey)
            .ExecuteCommandAsync(cancellationToken);
        if (rows.Count > 0)
        {
            await appDb.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        }

        task.MarkObjectProgress(nameof(RuntimeRoute), 1, completed, 0, userId, DateTimeOffset.UtcNow);
        await UpdateTaskAsync(task, cancellationToken);
        await AddSnapshotAsync(task, cancellationToken);
    }

    private sealed record SqlScriptDescriptor(string FileName, string FullPath);

    [SugarTable("SchemaMigrations")]
    private sealed class SchemaMigrationEntry
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(Length = 16)]
        public string Scope { get; set; } = string.Empty;

        [SugarColumn(Length = 128)]
        public string TargetKey { get; set; } = string.Empty;

        [SugarColumn(Length = 256)]
        public string ScriptName { get; set; } = string.Empty;

        [SugarColumn(Length = 64)]
        public string ChecksumSha256 { get; set; } = string.Empty;

        public DateTimeOffset ExecutedAt { get; set; }

        [SugarColumn(Length = 64)]
        public string ExecutedBy { get; set; } = string.Empty;
    }

    private sealed record SqliteRecoveryResult(bool Attempted, bool Success, string? FailureSummary)
    {
        public static SqliteRecoveryResult NotAttempted() => new(false, false, null);

        public static SqliteRecoveryResult Succeeded() => new(true, true, null);

        public static SqliteRecoveryResult Failed(string failureSummary) => new(true, false, failureSummary);
    }
}
