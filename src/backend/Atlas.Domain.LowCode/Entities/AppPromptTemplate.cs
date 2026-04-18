using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 提示词模板（M18 S18-5）。跨域资源：可在智能体 + 工作流 LLM 节点（M20）跨层复用。
/// </summary>
public sealed class AppPromptTemplate : TenantEntity
{
#pragma warning disable CS8618
    public AppPromptTemplate() : base(TenantId.Empty)
    {
        Code = string.Empty;
        Name = string.Empty;
        Body = string.Empty;
        Mode = "jinja";
    }
#pragma warning restore CS8618

    public AppPromptTemplate(TenantId tenantId, long id, string code, string name, string body, string? mode, long createdByUserId)
        : base(tenantId)
    {
        Id = id;
        Code = code;
        Name = name;
        Body = body;
        Mode = mode ?? "jinja";
        CreatedByUserId = createdByUserId;
        Version = "1.0.0";
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string Code { get; private set; }

    [SugarColumn(Length = 200, IsNullable = false)]
    public string Name { get; private set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string Body { get; private set; }

    /// <summary>jinja / markdown / plain。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Mode { get; private set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string Version { get; private set; }

    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? Description { get; private set; }

    /// <summary>shareScope：private / team / public（默认 private）。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string ShareScope { get; private set; } = "private";

    public long CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string name, string body, string? mode, string? description, string? shareScope)
    {
        Name = name;
        Body = body;
        Mode = mode ?? Mode;
        Description = description;
        ShareScope = shareScope ?? ShareScope;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// 插件定义（M18 S18-4）。与现有工作流 N10 插件节点共享 PluginRegistry，但本实体专注低代码 Studio 视角的市场/创建/计量。
/// </summary>
public sealed class LowCodePluginDefinition : TenantEntity
{
#pragma warning disable CS8618
    public LowCodePluginDefinition() : base(TenantId.Empty)
    {
        PluginId = string.Empty;
        Name = string.Empty;
        ToolsJson = "[]";
    }
#pragma warning restore CS8618

    public LowCodePluginDefinition(TenantId tenantId, long id, string pluginId, string name, string? description, long createdByUserId)
        : base(tenantId)
    {
        Id = id;
        PluginId = pluginId;
        Name = name;
        Description = description;
        ToolsJson = "[]";
        CreatedByUserId = createdByUserId;
        ShareScope = "private";
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        LatestVersion = "1.0.0";
    }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string PluginId { get; private set; }

    [SugarColumn(Length = 200, IsNullable = false)]
    public string Name { get; private set; }

    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? Description { get; private set; }

    /// <summary>OpenAPI / 手动定义工具 JSON。</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string ToolsJson { get; private set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string LatestVersion { get; private set; }

    /// <summary>private / team / public（市场可见性）。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string ShareScope { get; private set; }

    public long CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string name, string? description, string toolsJson, string? shareScope)
    {
        Name = name;
        Description = description;
        ToolsJson = toolsJson;
        ShareScope = shareScope ?? ShareScope;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void BumpVersion(string version)
    {
        LatestVersion = version;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>插件版本归档（M18 S18-4）。</summary>
public sealed class LowCodePluginVersion : TenantEntity
{
#pragma warning disable CS8618
    public LowCodePluginVersion() : base(TenantId.Empty)
    {
        PluginId = string.Empty;
        Version = string.Empty;
        ToolsJson = "[]";
    }
#pragma warning restore CS8618

    public LowCodePluginVersion(TenantId tenantId, long id, string pluginId, string version, string toolsJson, long publishedByUserId)
        : base(tenantId)
    {
        Id = id;
        PluginId = pluginId;
        Version = version;
        ToolsJson = toolsJson;
        PublishedByUserId = publishedByUserId;
        PublishedAt = DateTimeOffset.UtcNow;
    }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string PluginId { get; private set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string Version { get; private set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string ToolsJson { get; private set; }

    public long PublishedByUserId { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }
}

/// <summary>插件授权（M18 S18-4）。</summary>
public sealed class LowCodePluginAuthorization : TenantEntity
{
#pragma warning disable CS8618
    public LowCodePluginAuthorization() : base(TenantId.Empty)
    {
        PluginId = string.Empty;
        AuthKind = "api_key";
    }
#pragma warning restore CS8618

    public LowCodePluginAuthorization(TenantId tenantId, long id, string pluginId, string authKind, string? credentialEncrypted, long grantedByUserId)
        : base(tenantId)
    {
        Id = id;
        PluginId = pluginId;
        AuthKind = authKind;
        CredentialEncrypted = credentialEncrypted;
        GrantedByUserId = grantedByUserId;
        GrantedAt = DateTimeOffset.UtcNow;
    }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string PluginId { get; private set; }

    /// <summary>api_key / oauth / basic / none。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string AuthKind { get; private set; }

    /// <summary>加密后的凭据（M18 阶段简化 base64；M14 等保密钥加密接入后替换）。</summary>
    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? CredentialEncrypted { get; private set; }

    public long GrantedByUserId { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
}

/// <summary>插件调用计量（M18 S18-4）。</summary>
public sealed class LowCodePluginUsage : TenantEntity
{
#pragma warning disable CS8618
    public LowCodePluginUsage() : base(TenantId.Empty)
    {
        PluginId = string.Empty;
        Day = string.Empty;
    }
#pragma warning restore CS8618

    public LowCodePluginUsage(TenantId tenantId, long id, string pluginId, string day)
        : base(tenantId)
    {
        Id = id;
        PluginId = pluginId;
        Day = day;
    }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string PluginId { get; private set; }

    /// <summary>YYYY-MM-DD（按日聚合）。</summary>
    [SugarColumn(Length = 16, IsNullable = false)]
    public string Day { get; private set; }

    public long InvocationCount { get; private set; }
    public long ErrorCount { get; private set; }

    public void RecordInvocation(bool ok)
    {
        InvocationCount += 1;
        if (!ok) ErrorCount += 1;
    }
}
