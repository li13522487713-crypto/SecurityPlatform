using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Enums;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Infrastructure.DynamicTables;
using System.Text;
using System.Text.RegularExpressions;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class MigrationService : IMigrationService
{
    /// <summary>
    /// 固定大小锁池，避免按 tenant+table 无限增长导致内存泄漏。
    /// 不同 key 可能映射到同一锁，仅增加少量串行化，迁移操作本身低频可接受。
    /// </summary>
    private const int LockPoolSize = 64;
    private static readonly SemaphoreSlim[] LockPool = CreateLockPool();

    private static SemaphoreSlim[] CreateLockPool()
    {
        var pool = new SemaphoreSlim[LockPoolSize];
        for (var i = 0; i < LockPoolSize; i++)
        {
            pool[i] = new SemaphoreSlim(1, 1);
        }
        return pool;
    }

    private static SemaphoreSlim GetTableLock(string lockKey)
    {
        var hash = lockKey.GetHashCode(StringComparison.Ordinal) & 0x7FFFFFFF;
        return LockPool[hash % LockPoolSize];
    }

    private readonly IMigrationRecordRepository _migrationRecordRepository;
    private readonly IDynamicTableRepository _dynamicTableRepository;
    private readonly IDynamicFieldRepository _dynamicFieldRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly ISqlSugarClient _db;

    public MigrationService(
        IMigrationRecordRepository migrationRecordRepository,
        IDynamicTableRepository dynamicTableRepository,
        IDynamicFieldRepository dynamicFieldRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        ISqlSugarClient db)
    {
        _migrationRecordRepository = migrationRecordRepository;
        _dynamicTableRepository = dynamicTableRepository;
        _dynamicFieldRepository = dynamicFieldRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _db = db;
    }

    public async Task<PagedResult<MigrationRecordListItem>> QueryAsync(
        PagedRequest request,
        TenantId tenantId,
        string? tableKey,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _migrationRecordRepository.QueryPageAsync(
            tenantId,
            request.PageIndex,
            request.PageSize,
            request.Keyword,
            tableKey,
            cancellationToken);

        var listItems = items
            .Select(x => new MigrationRecordListItem(
                x.Id.ToString(),
                x.TableKey,
                x.Version,
                x.Status,
                x.IsDestructive,
                x.CreatedAt,
                x.UpdatedAt,
                x.ExecutedAt,
                x.CreatedBy,
                x.ErrorMessage))
            .ToArray();

        return new PagedResult<MigrationRecordListItem>(listItems, totalCount, request.PageIndex, request.PageSize);
    }

    public async Task<MigrationRecordDetail?> GetByIdAsync(
        TenantId tenantId,
        long migrationId,
        CancellationToken cancellationToken)
    {
        var entity = await _migrationRecordRepository.FindByIdAsync(tenantId, migrationId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return new MigrationRecordDetail(
            entity.Id.ToString(),
            entity.TableKey,
            entity.Version,
            entity.Status,
            entity.UpScript,
            entity.DownScript,
            entity.IsDestructive,
            entity.ErrorMessage,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.ExecutedAt,
            entity.CreatedBy,
            entity.UpdatedBy);
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        MigrationRecordCreateRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _dynamicTableRepository.FindByKeyAsync(tenantId, request.TableKey, null, cancellationToken);
        var dbType = table?.DbType ?? DynamicDbType.Sqlite;
        var validationError = MigrationScriptValidator.Validate(request.UpScript, request.TableKey, dbType);
        if (validationError is not null)
        {
            throw new BusinessException(ErrorCodes.ValidationError, validationError);
        }

        var existed = await _migrationRecordRepository.FindByVersionAsync(
            tenantId,
            request.TableKey,
            request.Version,
            cancellationToken);
        if (existed is not null)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "同一表的迁移版本已存在。");
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new MigrationRecord(
            tenantId,
            request.TableKey,
            request.Version,
            request.UpScript,
            request.DownScript,
            request.IsDestructive,
            userId,
            _idGeneratorAccessor.NextId(),
            now);

        await _migrationRecordRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task<MigrationScriptPreview> DetectChangesAsync(
        TenantId tenantId,
        string tableKey,
        DynamicTableAlterRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _dynamicTableRepository.FindByKeyAsync(tenantId, tableKey, null, cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "动态表不存在。");
        }

        var fields = await _dynamicFieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        var existing = fields.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var upSql = new StringBuilder();
        var downSql = new StringBuilder();
        var isDestructive = false;

        var hasExecutableUp = false;

        foreach (var field in request.AddFields)
        {
            if (existing.ContainsKey(field.Name))
            {
                warnings.Add($"字段 {field.Name} 已存在，已跳过新增。");
                continue;
            }

            var addOperation = BuildAddColumnOperationText(field);
            upSql.AppendLine(addOperation);
            hasExecutableUp = true;
            downSql.AppendLine("-- SQLite 需重建表，新增列的回退需手动执行");
        }

        foreach (var field in request.UpdateFields)
        {
            if (!existing.TryGetValue(field.Name, out var current))
            {
                warnings.Add($"字段 {field.Name} 不存在，已跳过更新。");
                continue;
            }

            if (!IsNoopUpdate(current, field))
            {
                isDestructive = true;
                warnings.Add($"字段 {field.Name} 涉及结构修改，SQLite 需重建表。");
                upSql.AppendLine("-- SQLite 需重建表，字段修改需手动执行");
                downSql.AppendLine("-- SQLite 需重建表，字段回退需手动执行");
            }
        }

        foreach (var fieldName in request.RemoveFields)
        {
            if (!existing.ContainsKey(fieldName))
            {
                warnings.Add($"字段 {fieldName} 不存在，已跳过删除。");
                continue;
            }

            isDestructive = true;
            warnings.Add($"字段 {fieldName} 删除属于破坏性变更，SQLite 需重建表。");
            upSql.AppendLine("-- SQLite 需重建表，字段删除需手动执行");
            downSql.AppendLine("-- SQLite 需重建表，字段恢复需手动执行");
        }

        var upScript = upSql.ToString().Trim();
        if (string.IsNullOrWhiteSpace(upScript))
        {
            upScript = "-- no-op: 未检测到可执行的结构变更";
        }
        else if (!hasExecutableUp)
        {
            upScript = "-- no-op: SQLite 需重建表的变更需手动执行";
        }

        var downScript = downSql.ToString().Trim();
        if (string.IsNullOrWhiteSpace(downScript))
        {
            downScript = "-- no-op: 无回退脚本";
        }
        else if (!hasExecutableUp)
        {
            downScript = "-- no-op: SQLite 需重建表的回退需手动执行";
        }

        return new MigrationScriptPreview(
            tableKey,
            upScript,
            downScript,
            isDestructive,
            warnings);
    }

    public async Task<MigrationExecutionResult> ExecuteAsync(
        TenantId tenantId,
        long userId,
        long migrationId,
        bool confirmDestructive,
        CancellationToken cancellationToken)
    {
        var migration = await _migrationRecordRepository.FindByIdAsync(tenantId, migrationId, cancellationToken);
        if (migration is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "迁移记录不存在。");
        }

        if (migration.IsDestructive && !confirmDestructive)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "该迁移包含破坏性变更，请先通过预检查并确认执行。");
        }

        var table = await _dynamicTableRepository.FindByKeyAsync(tenantId, migration.TableKey, null, cancellationToken);
        var dbType = table?.DbType ?? DynamicDbType.Sqlite;

        var validationError = MigrationScriptValidator.Validate(migration.UpScript, migration.TableKey, dbType);
        if (validationError is not null)
        {
            throw new BusinessException(ErrorCodes.ValidationError, validationError);
        }

        var lockKey = $"{tenantId.Value:D}:{migration.TableKey}";
        var tableLock = GetTableLock(lockKey);
        await tableLock.WaitAsync(cancellationToken);
        try
        {
            // 在锁内重新加载实体，确保使用最新状态
            migration = await _migrationRecordRepository.FindByIdAsync(tenantId, migrationId, cancellationToken);
            if (migration is null)
            {
                throw new BusinessException(ErrorCodes.NotFound, "迁移记录不存在。");
            }

            // 在临界区内检查状态，防止竞态条件
            if (string.Equals(migration.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException(ErrorCodes.ValidationError, "迁移已成功执行，不可重复执行。");
            }
            if (string.Equals(migration.Status, "Executing", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException(ErrorCodes.ValidationError, "迁移正在执行中，请稍后重试。");
            }

            var now = DateTimeOffset.UtcNow;
            migration.MarkExecuting(userId, now);
            await _migrationRecordRepository.UpdateAsync(migration, cancellationToken);

            try
            {
                // 安全边界：UpScript 已在执行前通过 MigrationScriptValidator 白名单校验，
                // 仅允许 ALTER TABLE ... ADD COLUMN，禁止 DROP/SELECT/INSERT 等任意 SQL。
                await ExecuteMigrationUpScriptAsync(table, migration.UpScript, cancellationToken);
                migration.MarkSucceeded(userId, DateTimeOffset.UtcNow);
            }
            catch (Exception ex)
            {
                migration.MarkFailed(ex.Message, userId, DateTimeOffset.UtcNow);
            }

            await _migrationRecordRepository.UpdateAsync(migration, cancellationToken);
            return new MigrationExecutionResult(
                migration.Id.ToString(),
                migration.TableKey,
                migration.Version,
                migration.Status,
                migration.ExecutedAt,
                migration.ErrorMessage);
        }
        finally
        {
            tableLock.Release();
        }
    }

    public async Task<MigrationPrecheckResult> PrecheckAsync(
        TenantId tenantId,
        long migrationId,
        CancellationToken cancellationToken)
    {
        var migration = await _migrationRecordRepository.FindByIdAsync(tenantId, migrationId, cancellationToken);
        if (migration is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "迁移记录不存在。");
        }

        var checks = new List<string>
        {
            "迁移记录存在",
            $"当前状态：{migration.Status}",
            $"目标表：{migration.TableKey}",
            $"版本：{migration.Version}"
        };

        if (migration.IsDestructive)
        {
            checks.Add("检测到破坏性变更，执行前需要确认");
        }

        var canExecute = string.Equals(migration.Status, "Draft", StringComparison.OrdinalIgnoreCase)
            || string.Equals(migration.Status, "Failed", StringComparison.OrdinalIgnoreCase);
        if (!canExecute)
        {
            if (string.Equals(migration.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                checks.Add("迁移已成功执行，不可重复执行");
            }
            else if (string.Equals(migration.Status, "Executing", StringComparison.OrdinalIgnoreCase))
            {
                checks.Add("当前迁移正在执行中，暂不可重复执行");
            }
        }

        return new MigrationPrecheckResult(
            migration.Id.ToString(),
            migration.TableKey,
            migration.Version,
            migration.IsDestructive,
            canExecute,
            checks);
    }

    private static string BuildAddColumnOperationText(DynamicFieldDefinition field)
    {
        var typeText = field.FieldType?.Trim() ?? "String";
        var nullableText = field.AllowNull ? "NULL" : "NOT NULL";
        return $"ADD COLUMN: {field.Name} ({typeText}, {nullableText})";
    }

    private async Task ExecuteMigrationUpScriptAsync(DynamicTable? table, string upScript, CancellationToken cancellationToken)
    {
        var lines = upScript
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("--", StringComparison.Ordinal))
            .ToArray();

        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var command = line.Trim();
            if (command.Length == 0)
            {
                continue;
            }

            if (command.EndsWith(";", StringComparison.Ordinal))
            {
                command = command[..^1];
            }

            var match = Regex.Match(
                command,
                @"^ALTER\s+TABLE\s+""?(?<table>[A-Za-z0-9_]+)""?\s+ADD\s+COLUMN\s+""?(?<column>[A-Za-z0-9_]+)""?\s+(?<type>[A-Za-z0-9()_,]+)(?<nullable>\s+NOT\s+NULL)?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (!match.Success)
            {
                continue;
            }

            var tableName = match.Groups["table"].Value;
            if (table is not null && !string.Equals(table.TableKey, tableName, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException(ErrorCodes.ValidationError, "迁移脚本表名与目标动态表不一致。");
            }

            var columnName = match.Groups["column"].Value;
            var columnType = match.Groups["type"].Value;
            var notNull = match.Groups["nullable"].Success;

            var nullClause = notNull ? " NOT NULL" : "";
            var addColumnSql = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnType}{nullClause}";
            await _db.Ado.ExecuteCommandAsync(addColumnSql);
        }
    }

    private static bool IsNoopUpdate(DynamicField current, DynamicFieldUpdateDefinition update)
    {
        var sameDisplayName = string.IsNullOrWhiteSpace(update.DisplayName) || string.Equals(update.DisplayName, current.DisplayName, StringComparison.Ordinal);
        var sameLength = !update.Length.HasValue || update.Length.Value == current.Length;
        var samePrecision = !update.Precision.HasValue || update.Precision.Value == current.Precision;
        var sameScale = !update.Scale.HasValue || update.Scale.Value == current.Scale;
        var sameAllowNull = !update.AllowNull.HasValue || update.AllowNull.Value == current.AllowNull;
        var sameUnique = !update.IsUnique.HasValue || update.IsUnique.Value == current.IsUnique;
        var sameDefaultValue = update.DefaultValue is null || string.Equals(update.DefaultValue, current.DefaultValue, StringComparison.Ordinal);

        return sameDisplayName && sameLength && samePrecision && sameScale && sameAllowNull && sameUnique && sameDefaultValue;
    }
}
