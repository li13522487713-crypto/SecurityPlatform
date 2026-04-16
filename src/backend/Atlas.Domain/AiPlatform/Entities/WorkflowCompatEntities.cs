using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class WorkflowCollaborator : TenantEntity
{
    public WorkflowCollaborator() : base(TenantId.Empty)
    {
        WorkflowId = string.Empty;
        UserId = string.Empty;
        DisplayName = string.Empty;
        RoleCode = "Editor";
    }

    public WorkflowCollaborator(TenantId tenantId, long id, string workflowId, string userId, string displayName, string roleCode)
        : base(tenantId)
    {
        Id = id;
        WorkflowId = workflowId;
        UserId = userId;
        DisplayName = displayName;
        RoleCode = roleCode;
        Enabled = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public string WorkflowId { get; private set; }
    public string UserId { get; private set; }
    public string DisplayName { get; private set; }
    public string RoleCode { get; private set; }
    public bool Enabled { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
}

public sealed class WorkflowTrigger : TenantEntity
{
    public WorkflowTrigger() : base(TenantId.Empty)
    {
        WorkflowId = string.Empty;
        Name = string.Empty;
        EventType = string.Empty;
    }

    public WorkflowTrigger(TenantId tenantId, long id, string workflowId, string name, string eventType)
        : base(tenantId)
    {
        Id = id;
        WorkflowId = workflowId;
        Name = name;
        EventType = eventType;
        Enabled = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public string WorkflowId { get; private set; }
    public string Name { get; private set; }
    public string EventType { get; private set; }
    public bool Enabled { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
}

public sealed class WorkflowJob : TenantEntity
{
    public WorkflowJob() : base(TenantId.Empty)
    {
        WorkflowId = string.Empty;
        Name = string.Empty;
        Status = "pending";
    }

    public WorkflowJob(TenantId tenantId, long id, string workflowId, string name)
        : base(tenantId)
    {
        Id = id;
        WorkflowId = workflowId;
        Name = name;
        Status = "pending";
        CreatedAtUtc = DateTime.UtcNow;
    }

    public string WorkflowId { get; private set; }
    public string Name { get; private set; }
    public string Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}

public sealed class WorkflowTask : TenantEntity
{
    public WorkflowTask() : base(TenantId.Empty)
    {
        JobId = string.Empty;
        Status = "pending";
    }

    public WorkflowTask(TenantId tenantId, long id, string jobId, string status, string? errorMessage = null)
        : base(tenantId)
    {
        Id = id;
        JobId = jobId;
        Status = status;
        ErrorMessage = errorMessage;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public string JobId { get; private set; }
    public string Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
}

public sealed class WorkflowConversationDef : TenantEntity
{
    public WorkflowConversationDef() : base(TenantId.Empty)
    {
        WorkflowId = string.Empty;
        Name = string.Empty;
    }

    public WorkflowConversationDef(TenantId tenantId, long id, string workflowId, string name)
        : base(tenantId)
    {
        Id = id;
        WorkflowId = workflowId;
        Name = name;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public string WorkflowId { get; private set; }
    public string Name { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
}

public sealed class ChatFlowRole : TenantEntity
{
    public ChatFlowRole() : base(TenantId.Empty)
    {
        WorkflowId = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
    }

    public ChatFlowRole(TenantId tenantId, long id, string workflowId, string name, string description, string? avatarUri)
        : base(tenantId)
    {
        Id = id;
        WorkflowId = workflowId;
        Name = name;
        Description = description;
        AvatarUri = avatarUri;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public string WorkflowId { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string? AvatarUri { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
}
