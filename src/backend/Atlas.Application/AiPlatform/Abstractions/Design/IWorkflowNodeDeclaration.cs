using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Application.AiPlatform.Abstractions;

/// <summary>
/// 工作流节点元数据声明（用于画布节点目录、表单配置与端口定义）。
/// </summary>
public interface IWorkflowNodeDeclaration
{
    WorkflowNodeType Type { get; }

    string Key { get; }

    string Name { get; }

    string Category { get; }

    string Description { get; }

    IReadOnlyList<WorkflowNodePortMetadata> Ports { get; }

    string ConfigSchemaJson { get; }

    WorkflowNodeUiMetadata UiMeta { get; }

    /// <summary>
    /// D9/K8：节点表单元数据（formMeta）—— JSON 数组，描述属性面板字段（label、type、enum 等）。
    /// 缺省 / null 时前端按 ConfigSchemaJson 生成默认表单。
    /// </summary>
    string? FormMetaJson { get; }
}
