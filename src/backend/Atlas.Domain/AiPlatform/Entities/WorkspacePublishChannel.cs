using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

/// <summary>
/// 工作空间内的发布渠道（PRD 04-4.6）。一个工作空间可绑定多个渠道，
/// 每个渠道可承载若干"可发布对象类型"（agent / app / workflow）。
/// </summary>
[SugarTable("WorkspacePublishChannel")]
public sealed class WorkspacePublishChannel : TenantEntity
{
    public WorkspacePublishChannel()
        : base(TenantId.Empty)
    {
        WorkspaceId = string.Empty;
        Name = string.Empty;
        ChannelType = string.Empty;
        Description = string.Empty;
        Status = "pending";
        AuthStatus = "unauthorized";
        SupportedTargetsJson = "[]";
        CreatedAt = DateTime.UtcNow;
    }

    public WorkspacePublishChannel(
        TenantId tenantId,
        string workspaceId,
        string name,
        string channelType,
        string description,
        string supportedTargetsJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Name = name;
        ChannelType = channelType;
        Description = description;
        SupportedTargetsJson = string.IsNullOrWhiteSpace(supportedTargetsJson) ? "[]" : supportedTargetsJson;
        Status = "pending";
        AuthStatus = "unauthorized";
        CreatedAt = DateTime.UtcNow;
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string WorkspaceId { get; private set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string Name { get; private set; }

    /// <summary>
    /// PRD 协议枚举：web-sdk / open-api / wechat / feishu / lark / custom。
    /// </summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string ChannelType { get; private set; }

    /// <summary>
    /// active / inactive / pending。
    /// </summary>
    [SugarColumn(Length = 16, IsNullable = false)]
    public string Status { get; private set; }

    /// <summary>
    /// authorized / expired / unauthorized。
    /// </summary>
    [SugarColumn(Length = 16, IsNullable = false)]
    public string AuthStatus { get; private set; }

    [SugarColumn(Length = 512, IsNullable = true)]
    public string Description { get; private set; }

    /// <summary>
    /// JSON 编码的支持目标类型数组：["agent","app","workflow"]。
    /// 第二阶段未引入对象绑定关系，前端按目标过滤即可。
    /// </summary>
    [SugarColumn(Length = 256, IsNullable = false)]
    public string SupportedTargetsJson { get; private set; }

    public DateTime? LastSyncAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public void Update(string? name, string? description, string? status, string? supportedTargetsJson)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name;
        }
        if (description is not null)
        {
            Description = description;
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            Status = status;
        }
        if (!string.IsNullOrWhiteSpace(supportedTargetsJson))
        {
            SupportedTargetsJson = supportedTargetsJson;
        }
    }

    public void MarkAuthorized()
    {
        AuthStatus = "authorized";
        Status = "active";
        LastSyncAt = DateTime.UtcNow;
    }
}
