namespace Atlas.Application.LowCode.Models;

/// <summary>
/// 渲染器能力差异化（M15 S15-2）。
/// 列出某 renderer 不支持的组件 type 列表，供前端 / Studio 设计期 / preview 渲染期降级。
/// </summary>
public sealed record RendererCapabilityDto(
    string Renderer,
    IReadOnlyList<string> UnsupportedComponentTypes,
    IReadOnlyList<RendererCapabilityHintDto> Hints);

public sealed record RendererCapabilityHintDto(string ComponentType, string Note);
