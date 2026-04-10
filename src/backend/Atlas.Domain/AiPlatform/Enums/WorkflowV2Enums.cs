namespace Atlas.Domain.AiPlatform.Enums;

/// <summary>
/// 工作流执行状态（V2 DAG 引擎）。
/// </summary>
public enum ExecutionStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
    Interrupted = 5
}

/// <summary>
/// 中断类型：当执行器需要人工介入或外部输入时使用。
/// </summary>
public enum InterruptType
{
    None = 0,
    QuestionAnswer = 1,
    ManualApproval = 2,
    Timeout = 3
}

/// <summary>
/// V2 工作流节点类型，编号与 Coze NodeType 对齐。
/// </summary>
public enum WorkflowNodeType
{
    Entry = 1,
    Exit = 2,
    Llm = 3,
    Plugin = 4,
    Agent = 60,
    CodeRunner = 5,
    KnowledgeRetriever = 6,
    Selector = 8,
    SubWorkflow = 9,
    OutputEmitter = 13,
    TextProcessor = 15,
    QuestionAnswer = 18,
    Break = 19,
    VariableAssignerWithinLoop = 20,
    Loop = 21,
    IntentDetector = 22,
    KnowledgeIndexer = 27,
    Batch = 28,
    Continue = 29,
    InputReceiver = 30,
    Comment = 31,
    VariableAggregator = 32,
    ConversationList = 53,
    MessageList = 37,
    ClearConversationHistory = 38,
    CreateConversation = 39,
    AssignVariable = 40,
    DatabaseCustomSql = 41,
    DatabaseUpdate = 42,
    DatabaseQuery = 43,
    DatabaseDelete = 44,
    HttpRequester = 45,
    DatabaseInsert = 46,
    ConversationUpdate = 51,
    ConversationDelete = 52,
    ConversationHistory = 54,
    CreateMessage = 55,
    EditMessage = 56,
    DeleteMessage = 57,
    JsonSerialization = 58,
    JsonDeserialization = 59,
    KnowledgeDeleter = 61,
    Ltm = 62
}

/// <summary>
/// 工作流生命周期状态。
/// </summary>
public enum WorkflowLifecycleStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

/// <summary>
/// 工作流模式：标准工作流 或 对话式工作流（ChatFlow）。
/// </summary>
public enum WorkflowMode
{
    Standard = 0,
    ChatFlow = 1
}
