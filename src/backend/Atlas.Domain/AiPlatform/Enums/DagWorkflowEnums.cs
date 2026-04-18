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
    Interrupted = 5,
    Skipped = 6,
    Blocked = 7
}

/// <summary>
/// 连线执行状态：用于回挂前端边样式。
/// </summary>
public enum EdgeExecutionStatus
{
    Idle = 0,
    Success = 1,
    Skipped = 2,
    Failed = 3,
    Incomplete = 4
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
    /// <summary>触发器创建/更新（M12 PLAN.md §M12 S12-3）。</summary>
    TriggerUpsert = 34,
    /// <summary>触发器读取/列表（M12）。</summary>
    TriggerRead = 35,
    /// <summary>触发器删除（M12）。</summary>
    TriggerDelete = 36,
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
    Ltm = 62,
    /// <summary>M20 上游对齐：单一变量读取节点（与 VariableAggregator(32) 区分）。</summary>
    Variable = 11,
    /// <summary>M20 上游对齐：图像生成（Coze ID 14）。</summary>
    ImageGenerate = 14,
    /// <summary>M20 上游对齐：图像参考（Coze ID 16）。</summary>
    ImageReference = 16,
    /// <summary>M20 上游对齐：图像画布（Coze ID 17 / 23）。</summary>
    ImageCanvas = 17,
    /// <summary>M20 上游对齐：场景变量（Coze ID 24）。</summary>
    SceneVariable = 24,
    /// <summary>M20 上游对齐：场景对话（Coze ID 25）。</summary>
    SceneChat = 25,
    /// <summary>M20 上游对齐：长期记忆（Coze ID 26）。原 Ltm(62) 保留兼容映射。</summary>
    LtmUpstream = 26,
    /// <summary>M20 内存读取（与 LtmUpstream/Ltm 联动；Coze 28 与 Atlas Batch(28) 冲突，使用私有 ID）。</summary>
    MemoryRead = 64,
    /// <summary>M20 内存写入。</summary>
    MemoryWrite = 65,
    /// <summary>M20 内存删除。</summary>
    MemoryDelete = 66,
    /// <summary>
    /// M20 图像生成（Atlas 私有 N44，与上游 ImageGenerate(14) 区分）。
    /// 注：Coze 节点编号 44 与 Atlas DatabaseDelete=44 冲突，私有节点改用 68。
    /// </summary>
    ImageGeneration = 68,
    /// <summary>M20 图像画布合成（Atlas 私有 N45）。注：Coze 编号 45 与 Atlas HttpRequester=45 冲突，私有节点改用 69。</summary>
    Canvas = 69,
    /// <summary>M20 图像插件（Atlas 私有 N46）。注：Coze 编号 46 与 Atlas DatabaseInsert=46 冲突，私有节点改用 70。</summary>
    ImagePlugin = 70,
    /// <summary>M20 视频生成（Atlas 私有 N47）。</summary>
    VideoGeneration = 47,
    /// <summary>M20 视频转音频（Atlas 私有 N48）。</summary>
    VideoToAudio = 48,
    /// <summary>M20 视频抽帧（Atlas 私有 N49）。</summary>
    VideoFrameExtraction = 49,
    /// <summary>
    /// M20 上游对齐：图像流（Coze ID 15）。
    /// 注：Coze 上游 ID 15 与 Atlas 现存 TextProcessor=15 存在枚举值冲突；为保持 DB 兼容
    /// 将 Atlas 私有 Imageflow 映射到 67（紧跟 MemoryDelete=66）。
    /// 上游 ID 15 由前端 mapper 在 schema 序列化阶段做单向 67↔15 映射。
    /// </summary>
    Imageflow = 67,
    /// <summary>D6：自然语言转 SQL/查询条件——通过 LLM 把自然语言转成 JSON clauses，再走标准 DatabaseQuery 执行。</summary>
    DatabaseNl2Sql = 71
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
