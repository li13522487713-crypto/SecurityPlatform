using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public class SqlQueryService : ISqlQueryService
{
    private const int QueryTimeoutSeconds = 30;
    private const int MaxPreviewRows = 500;

    private readonly ILogger<SqlQueryService> _logger;
    private readonly TenantDataSourceRepository _repository;
    private readonly DatabaseEncryptionOptions _encryptionOptions;

    private static readonly Regex ForbiddenKeywordsRegex = new(
        @"\b(INSERT|UPDATE|DELETE|DROP|ALTER|TRUNCATE|GRANT|REVOKE|EXEC|EXECUTE)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public SqlQueryService(
        ILogger<SqlQueryService> logger,
        TenantDataSourceRepository repository,
        IOptions<DatabaseEncryptionOptions> encryptionOptions)
    {
        _logger = logger;
        _repository = repository;
        _encryptionOptions = encryptionOptions.Value;
    }

    public async Task<SqlQueryResult> ExecutePreviewQueryAsync(
        string tenantIdValue,
        long dataSourceId,
        SqlQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new SqlQueryResult();
        var sw = Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(request.Sql))
            {
                result.Success = false;
                result.ErrorMessage = "SQL statement cannot be empty.";
                return result;
            }

            if (!request.Sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                result.Success = false;
                result.ErrorMessage = "Security Alert: Only SELECT statements are allowed in the preview mode.";
                return result;
            }

            if (ForbiddenKeywordsRegex.IsMatch(request.Sql))
            {
                result.Success = false;
                result.ErrorMessage = "Security Alert: Destructive SQL keywords detected and blocked.";
                return result;
            }

            using var db = await CreateClientAsync(tenantIdValue, dataSourceId, cancellationToken);
            if (db is null)
            {
                result.Success = false;
                result.ErrorMessage = "Data source not found.";
                return result;
            }

            db.Ado.CommandTimeOut = QueryTimeoutSeconds;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(QueryTimeoutSeconds));

            var dt = await db.Ado.GetDataTableAsync(request.Sql);

            result.Success = true;

            foreach (DataColumn col in dt.Columns)
            {
                result.Columns.Add(new SqlQueryResultColumn(col.ColumnName, col.ColumnName, col.DataType.Name));
            }

            var rowCount = Math.Min(dt.Rows.Count, MaxPreviewRows);
            for (var i = 0; i < rowCount; i++)
            {
                var row = dt.Rows[i];
                var dict = new Dictionary<string, object?>();
                foreach (DataColumn col in dt.Columns)
                {
                    dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                }

                result.Data.Add(dict);
            }
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = $"Query execution timed out after {QueryTimeoutSeconds} seconds.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing preview query on datasource {DataSourceId}.", dataSourceId);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            sw.Stop();
            result.ExecutionTimeMs = sw.ElapsedMilliseconds;
        }

        return result;
    }

    public async Task<DataSourceSchemaResult> GetSchemaAsync(
        string tenantIdValue,
        long dataSourceId,
        CancellationToken cancellationToken = default)
    {
        var result = new DataSourceSchemaResult();

        try
        {
            using var db = await CreateClientAsync(tenantIdValue, dataSourceId, cancellationToken);
            if (db is null)
            {
                result.Success = false;
                result.ErrorMessage = "Data source not found.";
                return result;
            }

            db.Ado.CommandTimeOut = QueryTimeoutSeconds;

            var tables = db.DbMaintenance.GetTableInfoList(false);

            foreach (var table in tables)
            {
                var tableInfo = new DataSourceTableInfo { Name = table.Name };

                var cols = db.DbMaintenance.GetColumnInfosByTableName(table.Name, false);
                tableInfo.Columns = cols.Select(c => new DataSourceColumnInfo
                {
                    Name = c.DbColumnName,
                    DataType = c.DataType ?? "unknown",
                    IsNullable = c.IsNullable,
                    IsPrimaryKey = c.IsPrimarykey
                }).ToList();

                result.Tables.Add(tableInfo);
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schema for datasource {DataSourceId}.", dataSourceId);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private async Task<SqlSugarClient?> CreateClientAsync(
        string tenantIdValue,
        long dataSourceId,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByTenantAndIdAsync(tenantIdValue, dataSourceId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var connectionString = _encryptionOptions.Enabled
            ? TenantDbConnectionFactory.Decrypt(entity.EncryptedConnectionString, _encryptionOptions.Key)
            : entity.EncryptedConnectionString;

        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = connectionString,
            DbType = MapDbType(entity.DbType),
            IsAutoCloseConnection = true
        });
    }

    private static SqlSugar.DbType MapDbType(string providerName)
    {
        return providerName?.ToLowerInvariant() switch
        {
            "sqlite" => SqlSugar.DbType.Sqlite,
            "mysql" => SqlSugar.DbType.MySql,
            "postgresql" => SqlSugar.DbType.PostgreSQL,
            "sqlserver" => SqlSugar.DbType.SqlServer,
            _ => throw new NotSupportedException($"Database provider {providerName} is not supported.")
        };
    }
}
