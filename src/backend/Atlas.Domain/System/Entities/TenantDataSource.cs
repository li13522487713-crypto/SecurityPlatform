using Atlas.Core.Abstractions;
using SqlSugar;

namespace Atlas.Domain.System.Entities;

/// <summary>
/// 租户数据源配置（等保2.0 数据隔离）
/// </summary>
public sealed class TenantDataSource : EntityBase
{
    public TenantDataSource() { }

    public TenantDataSource(
        string tenantIdValue,
        string name,
        string encryptedConnectionString,
        string dbType,
        long id,
        long? appId = null,
        int maxPoolSize = 50,
        int connectionTimeoutSeconds = 15)
    {
        Id = id;
        TenantIdValue = tenantIdValue;
        Name = name;
        EncryptedConnectionString = encryptedConnectionString;
        DbType = dbType;
        AppId = appId;
        MaxPoolSize = maxPoolSize;
        ConnectionTimeoutSeconds = connectionTimeoutSeconds;
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

    /// <summary>应用级数据源（null 表示平台级）</summary>
    [SugarColumn(IsNullable = true)]
    public long? AppId { get; set; }

    /// <summary>连接池上限</summary>
    public int MaxPoolSize { get; set; } = 50;

    /// <summary>连接超时（秒）</summary>
    public int ConnectionTimeoutSeconds { get; set; } = 15;

    /// <summary>最近一次连通性测试结果</summary>
    [SugarColumn(IsNullable = true)]
    public bool? LastTestSuccess { get; set; }

    /// <summary>最近一次连通性测试时间</summary>
    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? LastTestedAt { get; set; }

    /// <summary>最近一次测试消息（脱敏后）</summary>
    [SugarColumn(IsNullable = true)]
    public string? LastTestMessage { get; set; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? UpdatedAt { get; set; }

    public void Update(
        string name,
        string encryptedConnectionString,
        string dbType,
        int maxPoolSize,
        int connectionTimeoutSeconds)
    {
        Name = name;
        EncryptedConnectionString = encryptedConnectionString;
        DbType = dbType;
        MaxPoolSize = maxPoolSize;
        ConnectionTimeoutSeconds = connectionTimeoutSeconds;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public void MarkTestResult(bool success, string? message, DateTimeOffset testedAt)
    {
        LastTestSuccess = success;
        LastTestMessage = message;
        LastTestedAt = testedAt;
        UpdatedAt = testedAt;
    }
}
