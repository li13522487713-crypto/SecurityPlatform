using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Microflows.Runtime.Actions.Database;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.Microflows;

/// <summary>
/// 将 Atlas.Application.Microflows 的 IMicroflowDatabaseQueryService 桥接到
/// Atlas.Application 层的 IDatabaseManagementService，使微流 executor 不直接依赖
/// Infrastructure 层。
/// </summary>
public sealed class MicroflowDatabaseQueryService : IMicroflowDatabaseQueryService
{
    private readonly IDatabaseManagementService _databaseManagementService;

    public MicroflowDatabaseQueryService(IDatabaseManagementService databaseManagementService)
    {
        _databaseManagementService = databaseManagementService;
    }

    public async Task<MicroflowDatabaseQueryResult> ExecuteAsync(
        MicroflowDatabaseQueryRequest request,
        CancellationToken cancellationToken)
    {
        TenantId tenantId;
        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            tenantId = TenantId.Empty;
        }
        else if (Guid.TryParse(request.TenantId, out var tenantGuid))
        {
            tenantId = tenantGuid;
        }
        else
        {
            tenantId = TenantId.Empty;
        }

        var options = new DatabaseCenterSqlExecuteOptions
        {
            TimeoutSeconds = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 30,
            MaxRows = request.MaxRows > 0 ? request.MaxRows : 1000,
            Mode = request.Mode switch
            {
                MicroflowDatabaseQueryMode.SelectOnly => DatabaseSqlExecuteMode.SelectOnly,
                MicroflowDatabaseQueryMode.DmlOnly => DatabaseSqlExecuteMode.DmlOnly,
                _ => DatabaseSqlExecuteMode.Auto
            }
        };

        var dbParams = request.Parameters
            .Select(p => new DatabaseSqlParameter(p.Name, p.Value))
            .ToList();

        var result = await _databaseManagementService.ExecuteParameterizedAsync(
            tenantId,
            request.SourceId,
            request.Sql,
            dbParams,
            options,
            cancellationToken);

        return new MicroflowDatabaseQueryResult
        {
            Success = result.Success,
            ErrorMessage = result.ErrorMessage,
            Columns = result.Columns,
            Rows = result.Rows,
            AffectedRows = result.AffectedRows,
            ElapsedMs = result.ElapsedMs,
            Truncated = result.Truncated
        };
    }
}
