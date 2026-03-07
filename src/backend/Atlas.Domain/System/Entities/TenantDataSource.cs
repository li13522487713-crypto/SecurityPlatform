using Atlas.Core.Abstractions;

namespace Atlas.Domain.System.Entities;

/// <summary>
/// 租户数据源配置（等保2.0 数据隔离）
/// </summary>
public sealed class TenantDataSource : EntityBase
{
    public TenantDataSource() { }

    public TenantDataSource(string tenantIdValue, string name, string encryptedConnectionString, string dbType, long id)
    {
        Id = id;
        TenantIdValue = tenantIdValue;
        Name = name;
        EncryptedConnectionString = encryptedConnectionString;
        DbType = dbType;
        MaxPoolSize = 100;
        ConnectionTimeoutSeconds = 30;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>所属租户 ID</summary>
    public string TenantIdValue { get; set; } = string.Empty;

    /// <summary>数据源名称（描述）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>加密存储的连接字符串（AES-256）</summary>
    public string EncryptedConnectionString { get; set; } = string.Empty;

    /// <summary>数据库类型：SQLite / SqlServer</summary>
    public string DbType { get; set; } = "SQLite";

    /// <summary>所属应用 ID，null 表示平台级数据源</summary>
    public long? AppId { get; set; }

    /// <summary>连接池最大连接数</summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>连接超时（秒）</summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>最近一次连接测试是否成功</summary>
    public bool? LastTestSuccess { get; set; }

    /// <summary>最近一次连接测试时间</summary>
    public DateTimeOffset? LastTestedAt { get; set; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public void Update(string name, string encryptedConnectionString, string dbType)
    {
        Name = name;
        EncryptedConnectionString = encryptedConnectionString;
        DbType = dbType;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordTestResult(bool success)
    {
        LastTestSuccess = success;
        LastTestedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
