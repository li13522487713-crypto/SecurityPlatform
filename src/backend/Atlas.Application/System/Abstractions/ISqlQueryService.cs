using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.System.Models;

namespace Atlas.Application.System.Abstractions;

public interface ISqlQueryService
{
    Task<SqlQueryResult> ExecutePreviewQueryAsync(string tenantIdValue, long dataSourceId, SqlQueryRequest request, CancellationToken cancellationToken = default);

    Task<DataSourceSchemaResult> GetSchemaAsync(string tenantIdValue, long dataSourceId, CancellationToken cancellationToken = default);
}
