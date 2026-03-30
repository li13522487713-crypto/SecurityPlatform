using Atlas.Application.System.Models;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

internal static class DataSourceDriverRegistry
{
    private static readonly IReadOnlyList<DataSourceDriverDefinition> Definitions =
    [
        Build("SQLite", "SQLite", "Data Source=atlas.db;", false),
        Build("SqlServer", "SQL Server", "Server=127.0.0.1;Database=atlas;User Id=sa;Password=your_password;TrustServerCertificate=True;"),
        Build("MySql", "MySQL", "Server=127.0.0.1;Port=3306;Database=atlas;Uid=root;Pwd=your_password;"),
        Build("PostgreSQL", "PostgreSQL", "Host=127.0.0.1;Port=5432;Database=atlas;Username=postgres;Password=your_password;"),
        Build("Oracle", "Oracle", "Data Source=127.0.0.1:1521/ORCLCDB;User Id=atlas;Password=your_password;"),
        Build("Dm", "Dameng", "Server=127.0.0.1;Port=5236;Database=atlas;User Id=SYSDBA;Password=your_password;"),
        Build("Kdbndp", "KingbaseES", "Server=127.0.0.1;Port=54321;Database=atlas;User Id=system;Password=your_password;"),
        Build("Oscar", "Oscar", "Server=127.0.0.1;Port=2003;Database=atlas;User Id=oscar;Password=your_password;"),
        Build("Access", "Access", "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=atlas.accdb;", false)
    ];

    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["postgres"] = "PostgreSQL",
        ["postgresql"] = "PostgreSQL",
        ["pgsql"] = "PostgreSQL",
        ["sqlserver"] = "SqlServer",
        ["mssql"] = "SqlServer",
        ["mysql"] = "MySql",
        ["sqlite"] = "SQLite",
        ["oracle"] = "Oracle",
        ["dameng"] = "Dm",
        ["dm"] = "Dm",
        ["kingbase"] = "Kdbndp",
        ["kingbasees"] = "Kdbndp",
        ["oscar"] = "Oscar",
        ["access"] = "Access"
    };

    public static IReadOnlyList<DataSourceDriverDefinition> GetDefinitions() => Definitions;

    public static string NormalizeDriverCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "SQLite";
        }

        var trimmed = value.Trim();
        if (Aliases.TryGetValue(trimmed, out var canonical))
        {
            return canonical;
        }

        foreach (var def in Definitions)
        {
            if (string.Equals(def.Code, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return def.Code;
            }
        }

        return trimmed;
    }

    public static DbType ResolveDbType(string? driverCode)
    {
        var normalized = NormalizeDriverCode(driverCode);
        if (Enum.TryParse<DbType>(normalized, true, out var parsed))
        {
            return parsed;
        }

        throw new NotSupportedException($"Database provider {driverCode} is not supported.");
    }

    public static string ResolveConnectionString(
        string driverCode,
        string mode,
        string? rawConnectionString,
        Dictionary<string, string>? visualConfig)
    {
        if (string.Equals(mode, "visual", StringComparison.OrdinalIgnoreCase))
        {
            return BuildVisualConnectionString(driverCode, visualConfig);
        }

        return rawConnectionString?.Trim() ?? string.Empty;
    }

    private static string BuildVisualConnectionString(string driverCode, Dictionary<string, string>? visualConfig)
    {
        visualConfig ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var normalized = NormalizeDriverCode(driverCode);
        if (string.Equals(normalized, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            return $"Data Source={ReadRequired(visualConfig, "database")};";
        }

        var host = ReadRequired(visualConfig, "host");
        var port = ReadOptional(visualConfig, "port");
        var database = ReadRequired(visualConfig, "database");
        var username = ReadRequired(visualConfig, "username");
        var password = ReadRequired(visualConfig, "password");
        var extra = ReadOptional(visualConfig, "extra");

        var segments = new List<string>();
        switch (normalized)
        {
            case "SqlServer":
                segments.Add($"Server={host}{(string.IsNullOrWhiteSpace(port) ? string.Empty : $",{port}")}");
                segments.Add($"Database={database}");
                segments.Add($"User Id={username}");
                segments.Add($"Password={password}");
                segments.Add("TrustServerCertificate=True");
                break;
            case "MySql":
                segments.Add($"Server={host}");
                if (!string.IsNullOrWhiteSpace(port))
                {
                    segments.Add($"Port={port}");
                }
                segments.Add($"Database={database}");
                segments.Add($"Uid={username}");
                segments.Add($"Pwd={password}");
                break;
            case "PostgreSQL":
                segments.Add($"Host={host}");
                if (!string.IsNullOrWhiteSpace(port))
                {
                    segments.Add($"Port={port}");
                }
                segments.Add($"Database={database}");
                segments.Add($"Username={username}");
                segments.Add($"Password={password}");
                break;
            case "Oracle":
                segments.Add($"Data Source={host}{(string.IsNullOrWhiteSpace(port) ? string.Empty : $":{port}")}/{database}");
                segments.Add($"User Id={username}");
                segments.Add($"Password={password}");
                break;
            default:
                // 其它驱动保持统一连接参数拼装，允许通过 extra 附加方言项。
                segments.Add($"Server={host}");
                if (!string.IsNullOrWhiteSpace(port))
                {
                    segments.Add($"Port={port}");
                }
                segments.Add($"Database={database}");
                segments.Add($"User Id={username}");
                segments.Add($"Password={password}");
                break;
        }

        if (!string.IsNullOrWhiteSpace(extra))
        {
            segments.Add(extra.Trim().TrimEnd(';'));
        }

        return string.Join(';', segments) + ";";
    }

    private static string ReadRequired(Dictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"可视化连接配置缺少必填字段：{key}");
        }

        return value.Trim();
    }

    private static string ReadOptional(Dictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value?.Trim() ?? string.Empty : string.Empty;
    }

    private static DataSourceDriverDefinition Build(string code, string displayName, string example, bool supportsVisual = true)
    {
        var fields = supportsVisual
            ? new[]
            {
                new DataSourceDriverFieldDefinition("host", "Host", "text", true, false, false, "127.0.0.1", null),
                new DataSourceDriverFieldDefinition("port", "Port", "text", false, false, false, null, null),
                new DataSourceDriverFieldDefinition("database", "Database", "text", true, false, false, null, null),
                new DataSourceDriverFieldDefinition("username", "Username", "text", true, false, false, null, null),
                new DataSourceDriverFieldDefinition("password", "Password", "password", true, true, false, null, null),
                new DataSourceDriverFieldDefinition("extra", "Extra", "textarea", false, false, true, "如 SSL Mode=Required", null)
            }
            : new[]
            {
                new DataSourceDriverFieldDefinition("database", "Database", "text", true, false, false, "atlas.db", null),
                new DataSourceDriverFieldDefinition("extra", "Extra", "textarea", false, false, true, null, null)
            };

        return new DataSourceDriverDefinition(code, displayName, supportsVisual, example, fields);
    }
}
