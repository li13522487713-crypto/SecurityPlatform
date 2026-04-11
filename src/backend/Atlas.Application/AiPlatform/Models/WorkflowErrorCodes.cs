namespace Atlas.Application.AiPlatform.Models;

/// <summary>
/// Workflow V2 特定错误码，用于 API 响应中的结构化错误标识。
/// </summary>
public static class WorkflowErrorCodes
{
    // ── 资源不存在 ──────────────────────────────────────────────
    public const string WorkflowNotFound = "WORKFLOW_NOT_FOUND";
    public const string WorkflowVersionNotFound = "WORKFLOW_VERSION_NOT_FOUND";
    public const string WorkflowDraftNotFound = "WORKFLOW_DRAFT_NOT_FOUND";
    public const string WorkflowExecutionNotFound = "WORKFLOW_EXECUTION_NOT_FOUND";
    public const string WorkflowNodeExecutionNotFound = "WORKFLOW_NODE_EXECUTION_NOT_FOUND";

    // ── 画布校验 ──────────────────────────────────────────────
    public const string InvalidCanvas = "INVALID_CANVAS";
    public const string CanvasParseFailed = "CANVAS_PARSE_FAILED";
    public const string CanvasHasNoNodes = "CANVAS_HAS_NO_NODES";
    public const string CanvasMissingStartNode = "CANVAS_MISSING_START_NODE";
    public const string CanvasMissingEndNode = "CANVAS_MISSING_END_NODE";
    public const string CanvasCycleDetected = "CANVAS_CYCLE_DETECTED";
    public const string CanvasOrphanedNodes = "CANVAS_ORPHANED_NODES";

    // ── 执行状态 ──────────────────────────────────────────────
    public const string WorkflowNotPublished = "WORKFLOW_NOT_PUBLISHED";
    public const string ExecutionAlreadyRunning = "EXECUTION_ALREADY_RUNNING";
    public const string ExecutionCannotResume = "EXECUTION_CANNOT_RESUME";
    public const string ExecutionCannotCancel = "EXECUTION_CANNOT_CANCEL";
    public const string ExecutionTimeout = "EXECUTION_TIMEOUT";
    public const string ExecutionNodeFailed = "EXECUTION_NODE_FAILED";
    public const string ExecutionExpressionError = "EXECUTION_EXPRESSION_ERROR";

    // ── 版本与发布 ──────────────────────────────────────────────
    public const string WorkflowAlreadyPublished = "WORKFLOW_ALREADY_PUBLISHED";
    public const string WorkflowVersionMismatch = "WORKFLOW_VERSION_MISMATCH";
    public const string WorkflowPublishConflict = "WORKFLOW_PUBLISH_CONFLICT";

    // ── 配置与权限 ──────────────────────────────────────────────
    public const string InvalidNodeConfiguration = "INVALID_NODE_CONFIGURATION";
    public const string UnsupportedNodeType = "UNSUPPORTED_NODE_TYPE";
    public const string SubWorkflowDepthExceeded = "SUB_WORKFLOW_DEPTH_EXCEEDED";
    public const string LoopIterationsExceeded = "LOOP_ITERATIONS_EXCEEDED";
}
