namespace Atlas.Application.AiPlatform.Models;

public enum WorkflowNodePortDirection
{
    Input = 1,
    Output = 2
}

public sealed record WorkflowNodePortMetadata(
    string Key,
    string Name,
    WorkflowNodePortDirection Direction,
    string DataType,
    bool IsRequired = false,
    int MaxConnections = 1);

public sealed record WorkflowNodeUiMetadata(
    string Icon,
    string Color,
    bool SupportsBatch = false,
    int DefaultWidth = 360,
    int DefaultHeight = 120);
