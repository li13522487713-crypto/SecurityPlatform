using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class ConversationSection : TenantEntity
{
    public ConversationSection() : base(TenantId.Empty)
    {
    }

    public long ConversationId { get; private set; }
    public int Sequence { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

public sealed class ChatRunRecord : TenantEntity
{
    public ChatRunRecord() : base(TenantId.Empty)
    {
        OwnerType = string.Empty;
        SourceType = ChatRunSourceType.AgentDebug;
        Status = ChatRunStatus.Pending;
        InputJson = "{}";
        OutputJson = "{}";
        TraceId = string.Empty;
    }

    public long ConversationId { get; private set; }
    public long? SectionId { get; private set; }
    public string OwnerType { get; private set; }
    public long OwnerId { get; private set; }
    public ChatRunSourceType SourceType { get; private set; }
    public ChatRunStatus Status { get; private set; }
    public string InputJson { get; private set; }
    public string OutputJson { get; private set; }
    public string TraceId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
}

public enum ChatRunSourceType
{
    AgentDebug = 0,
    AgentOpenApi = 1,
    WorkflowDebug = 2,
    WorkflowOpenApi = 3,
    AppConversation = 4
}

public enum ChatRunStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
    Interrupted = 5
}

public sealed class ChatRunEvent : TenantEntity
{
    public ChatRunEvent() : base(TenantId.Empty)
    {
        EventType = string.Empty;
        PayloadJson = "{}";
    }

    public long RunRecordId { get; private set; }
    public string EventType { get; private set; }
    public string PayloadJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
