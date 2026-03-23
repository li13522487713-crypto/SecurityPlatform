using Atlas.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Atlas.Infrastructure.Configuration;

/// <summary>
/// 读取数据库中平台级（AppId 为空）系统参数并注入 IConfiguration。
/// </summary>
public sealed class DatabaseConfigurationProvider : ConfigurationProvider
{
    private readonly string _connectionString;
    private readonly string _platformTenantId;
    private readonly bool _encryptionEnabled;
    private readonly string _encryptionKey;

    public DatabaseConfigurationProvider(
        string connectionString,
        string platformTenantId,
        bool encryptionEnabled,
        string encryptionKey)
    {
        _connectionString = connectionString;
        _platformTenantId = platformTenantId;
        _encryptionEnabled = encryptionEnabled;
        _encryptionKey = encryptionKey;
    }

    public override void Load()
    {
        if (string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(_platformTenantId))
        {
            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        Data = LoadData();
    }

    public void ReloadFromDatabase()
    {
        Load();
        OnReload();
    }

    private Dictionary<string, string?> LoadData()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            if (!TableExists(connection, "SystemConfig"))
            {
                return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }

            var hasAppId = ColumnExists(connection, "SystemConfig", "AppId");
            var hasIsEncrypted = ColumnExists(connection, "SystemConfig", "IsEncrypted");
            var commandText = BuildQuery(hasAppId, hasIsEncrypted);
            using var command = connection.CreateCommand();
            command.CommandText = commandText;
            var tenantParameter = command.CreateParameter();
            tenantParameter.ParameterName = "@tenantId";
            tenantParameter.Value = _platformTenantId;
            command.Parameters.Add(tenantParameter);

            using var reader = command.ExecuteReader();
            var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                var key = reader.GetString(0);
                var value = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                var encrypted = !reader.IsDBNull(2) && reader.GetInt64(2) > 0;
                result[key] = encrypted ? Decrypt(value) : value;
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private string Decrypt(string cipherText)
    {
        if (!_encryptionEnabled || string.IsNullOrWhiteSpace(_encryptionKey) || string.IsNullOrEmpty(cipherText))
        {
            return cipherText;
        }

        try
        {
            return TenantDbConnectionFactory.Decrypt(cipherText, _encryptionKey);
        }
        catch
        {
            return cipherText;
        }
    }

    private static string BuildQuery(bool hasAppId, bool hasIsEncrypted)
    {
        var encryptedSelection = hasIsEncrypted ? "IsEncrypted" : "0 AS IsEncrypted";
        if (hasAppId)
        {
            return $"""
                SELECT ConfigKey, ConfigValue, {encryptedSelection}
                FROM SystemConfig
                WHERE TenantIdValue = @tenantId
                  AND (AppId IS NULL OR trim(AppId) = '')
                ORDER BY Id ASC
                """;
        }

        return $"""
            SELECT ConfigKey, ConfigValue, {encryptedSelection}
            FROM SystemConfig
            WHERE TenantIdValue = @tenantId
            ORDER BY Id ASC
            """;
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name LIMIT 1;";
        command.Parameters.AddWithValue("$name", tableName);
        var scalar = command.ExecuteScalar();
        return scalar is not null && scalar != DBNull.Value;
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
