using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

/// <summary>D4：AI 数据库配额校验。硬上限抛 BusinessException；软上限仅日志告警。</summary>
public sealed class AiDatabaseQuotaPolicy
{
    private readonly AiDatabaseQuotaOptions _options;
    private readonly AiDatabaseRepository _databaseRepository;
    private readonly AiDatabaseRecordRepository _recordRepository;
    private readonly ILogger<AiDatabaseQuotaPolicy> _logger;

    public AiDatabaseQuotaPolicy(
        IOptions<AiDatabaseQuotaOptions> options,
        AiDatabaseRepository databaseRepository,
        AiDatabaseRecordRepository recordRepository,
        ILogger<AiDatabaseQuotaPolicy> logger)
    {
        _options = options.Value;
        _databaseRepository = databaseRepository;
        _recordRepository = recordRepository;
        _logger = logger;
    }

    public AiDatabaseQuotaOptions Options => _options;

    public async Task EnsureCanCreateDatabaseAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var query = await _databaseRepository.GetPagedAsync(tenantId, keyword: null, pageIndex: 1, pageSize: 1, cancellationToken);
        if (query.Total >= _options.MaxPerTenant)
        {
            throw new BusinessException(
                $"租户数据库数量已达上限（{_options.MaxPerTenant}）。",
                ErrorCodes.ValidationError);
        }
    }

    public void EnsureFieldCount(int fieldCount)
    {
        if (fieldCount > _options.MaxFieldsPerTable)
        {
            throw new BusinessException(
                $"字段数量超过上限（{_options.MaxFieldsPerTable}）。",
                ErrorCodes.ValidationError);
        }
    }

    public void EnsureBulkInsertSize(int rowCount)
    {
        if (rowCount > _options.MaxBulkInsertRows)
        {
            throw new BusinessException(
                $"批量写入行数超过上限（{_options.MaxBulkInsertRows}）。",
                ErrorCodes.ValidationError);
        }
    }

    public async Task EnsureCanAddRowAsync(TenantId tenantId, long databaseId, int incoming, CancellationToken cancellationToken)
    {
        var existing = await _recordRepository.CountByDatabaseAsync(tenantId, databaseId, cancellationToken);
        var projected = existing + incoming;
        if (projected > _options.MaxRowsPerTable)
        {
            // 软上限：默认仅日志，不抛异常。后续可改为强制 by env config。
            _logger.LogWarning(
                "AI 数据库 {DatabaseId} 行数 {Projected} 超过软上限 {Limit}",
                databaseId,
                projected,
                _options.MaxRowsPerTable);
        }
    }
}
