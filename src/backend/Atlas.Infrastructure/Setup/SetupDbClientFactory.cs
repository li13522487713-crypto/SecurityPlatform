using Atlas.Core.Setup;
using Atlas.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Setup;

/// <summary>
/// Setup 专用 ISqlSugarClient 工厂。
/// 直接构建 SqlSugarScope，不经过 ISetupStateProvider.IsReady 门禁。
/// EntityService 配置与正常业务态工厂保持一致。
/// </summary>
public sealed class SetupDbClientFactory : ISetupDbClientFactory
{
    private readonly ILogger<SetupDbClientFactory> _logger;

    public SetupDbClientFactory(ILogger<SetupDbClientFactory> logger)
    {
        _logger = logger;
    }

    public ISqlSugarClient Create(string connectionString, string dbType)
    {
        var resolvedDbType = ResolveDbType(dbType);

        var config = new ConnectionConfig
        {
            ConnectionString = connectionString,
            DbType = resolvedDbType,
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

                    if (property.DeclaringType == typeof(Atlas.Domain.Identity.Entities.UserAccount))
                    {
                        if (property.PropertyType == typeof(DateTimeOffset))
                        {
                            column.IsIgnore = true;
                        }
                    }

                    if (property.DeclaringType == typeof(Atlas.Domain.Identity.Entities.AuthSession) &&
                        property.Name == nameof(Atlas.Domain.Identity.Entities.AuthSession.RevokedAt))
                    {
                        column.IsNullable = true;
                    }

                    if (property.DeclaringType == typeof(Atlas.Domain.Identity.Entities.RefreshToken))
                    {
                        if (property.Name == nameof(Atlas.Domain.Identity.Entities.RefreshToken.RevokedAt) ||
                            property.Name == nameof(Atlas.Domain.Identity.Entities.RefreshToken.ReplacedById))
                        {
                            column.IsNullable = true;
                        }
                    }

                    if (property.DeclaringType == typeof(Atlas.Domain.Approval.Entities.ApprovalTask)
                        && property.Name == nameof(Atlas.Domain.Approval.Entities.ApprovalTask.RowVersion))
                    {
                        column.IsEnableUpdateVersionValidation = true;
                    }
                }
            }
        };

        var db = new SqlSugarScope(config);

        if (resolvedDbType == DbType.Sqlite)
        {
            try
            {
                db.Ado.ExecuteCommand("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SetupDbClientFactory] SQLite PRAGMA 执行异常，尝试清理后重试");
                try
                {
                    SqliteSchemaAlignment.CleanupBrokenSchemaEntries(db);
                    db.Ado.ExecuteCommand("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;");
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "[SetupDbClientFactory] SQLite PRAGMA 重试仍然失败");
                }
            }
        }

        _logger.LogInformation("[SetupDbClientFactory] 已创建 setup 专用数据库连接，DbType={DbType}", dbType);
        return db;
    }

    private static DbType ResolveDbType(string? dbType)
    {
        try
        {
            return DataSourceDriverRegistry.ResolveDbType(dbType);
        }
        catch
        {
            return DbType.Sqlite;
        }
    }
}
