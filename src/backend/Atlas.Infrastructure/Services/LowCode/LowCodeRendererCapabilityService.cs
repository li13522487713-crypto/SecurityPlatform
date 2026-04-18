using Atlas.Application.LowCode.Models;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// 渲染器能力查询（M15 S15-2）。
/// 与前端 @atlas/lowcode-components-mini/MINI_CAPABILITY_MATRIX 完全一致。
/// </summary>
public interface ILowCodeRendererCapabilityService
{
    RendererCapabilityDto GetCapability(string renderer);
}

public sealed class LowCodeRendererCapabilityService : ILowCodeRendererCapabilityService
{
    /// <summary>component type → 支持的 renderer 集合（与前端一致）。</summary>
    private static readonly Dictionary<string, string[]> Matrix = new(StringComparer.Ordinal)
    {
        ["CodeEditor"] = Array.Empty<string>(),
        ["Chart"] = new[] { "h5" },
        ["WaterfallList"] = new[] { "mini-wx", "mini-douyin", "h5" },
        ["AiAvatarReply"] = new[] { "h5" },
        ["ColorPicker"] = new[] { "h5" },
        ["Drawer"] = new[] { "mini-wx", "h5" },
        ["Modal"] = new[] { "mini-wx", "mini-douyin", "h5" }
    };

    public RendererCapabilityDto GetCapability(string renderer)
    {
        var unsupported = new List<string>();
        var hints = new List<RendererCapabilityHintDto>();
        if (string.Equals(renderer, "web", StringComparison.OrdinalIgnoreCase))
        {
            // web 端默认全支持
            return new RendererCapabilityDto(renderer, unsupported, hints);
        }
        foreach (var (type, supported) in Matrix)
        {
            if (!supported.Contains(renderer, StringComparer.OrdinalIgnoreCase))
            {
                unsupported.Add(type);
                hints.Add(new RendererCapabilityHintDto(type, $"组件 {type} 在 {renderer} 上不可用，将渲染为 fallback"));
            }
        }
        return new RendererCapabilityDto(renderer, unsupported, hints);
    }
}
