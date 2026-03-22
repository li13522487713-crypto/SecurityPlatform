using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Domain.DynamicTables.Enums;

namespace Atlas.Infrastructure.DynamicTables;

internal static class DynamicEnumMapper
{
    public static DynamicDbType ParseDbType(string dbType)
    {
        if (string.IsNullOrWhiteSpace(dbType))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "Database type is required.");
        }

        return dbType.Trim().ToLowerInvariant() switch
        {
            "sqlite" => DynamicDbType.Sqlite,
            "sqlserver" => DynamicDbType.SqlServer,
            "mysql" => DynamicDbType.MySql,
            "postgresql" => DynamicDbType.PostgreSql,
            _ => throw new BusinessException(ErrorCodes.ValidationError, "Database type is not supported.")
        };
    }

    public static DynamicFieldType ParseFieldType(string fieldType)
    {
        if (string.IsNullOrWhiteSpace(fieldType))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "Field type is required.");
        }

        return fieldType.Trim().ToLowerInvariant() switch
        {
            "int" => DynamicFieldType.Int,
            "long" => DynamicFieldType.Long,
            "decimal" => DynamicFieldType.Decimal,
            "string" => DynamicFieldType.String,
            "text" => DynamicFieldType.Text,
            "bool" => DynamicFieldType.Bool,
            "datetime" => DynamicFieldType.DateTime,
            "date" => DynamicFieldType.Date,
            "time" => DynamicFieldType.Time,
            "enum" => DynamicFieldType.Enum,
            "file" => DynamicFieldType.File,
            "image" => DynamicFieldType.Image,
            "json" => DynamicFieldType.Json,
            "guid" => DynamicFieldType.Guid,
            _ => throw new BusinessException(ErrorCodes.ValidationError, "Field type is not supported.")
        };
    }

    public static DynamicTableStatus ParseStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return DynamicTableStatus.Active;
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "draft" => DynamicTableStatus.Draft,
            "active" => DynamicTableStatus.Active,
            "disabled" => DynamicTableStatus.Disabled,
            _ => DynamicTableStatus.Active
        };
    }
}
