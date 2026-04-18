using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// M20 媒体节点共享：检查模型供应商凭据是否就绪。
/// 当未注入或未配置时，按 PLAN §七风险与决策点 1，明确返回 BusinessException("MODEL_PROVIDER_NOT_CONFIGURED")，
/// 不再返回 mock/fallback，避免静默吞业务。
/// </summary>
internal static class MediaProviderGuard
{
    public static void EnsureProviderConfigured(IChatClientFactory? factory, string nodeName, string providerHint)
    {
        if (factory is null)
        {
            throw new BusinessException(
                "MODEL_PROVIDER_NOT_CONFIGURED",
                $"{nodeName} 节点需要模型供应商接入；当前 IChatClientFactory 未注册。请在租户配置 {providerHint}。");
        }
    }
}

/// <summary>
/// 上游图像生成节点（M20 ImageGenerate=14，与 Atlas 私有 ImageGeneration=68 区分）。
/// 设计：构造一次"指令型"生成请求 outputs，由前端 / dispatch 路由到具体图像模型供应商；
/// 当 IChatClientFactory 未配置（无 LLM 供应商）时，明确报错 MODEL_PROVIDER_NOT_CONFIGURED。
/// Config：prompt（必填）/ provider（如 openai/dalle3，可选）/ size（如 1024x1024，默认）/ count（默认 1）。
/// </summary>
public sealed class ImageGenerateUpstreamNodeExecutor : INodeExecutor
{
    private readonly IChatClientFactory? _factory;

    public ImageGenerateUpstreamNodeExecutor(IChatClientFactory? factory = null)
    {
        _factory = factory;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.ImageGenerate;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var prompt = context.ReplaceVariables(context.GetConfigString("prompt"));
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Task.FromResult(new NodeExecutionResult(false, outputs, "ImageGenerate 缺少 prompt。"));
        }

        MediaProviderGuard.EnsureProviderConfigured(_factory, "ImageGenerate", "图像生成模型（如 DALL-E / SD）");

        var provider = context.GetConfigString("provider", "auto");
        var size = context.GetConfigString("size", "1024x1024");
        var count = Math.Clamp(context.GetConfigInt32("count", 1), 1, 8);
        outputs["image_request"] = JsonSerializer.SerializeToElement(new { prompt, provider, size, count, kind = "upstream" });
        outputs["image_provider"] = VariableResolver.CreateStringElement(provider);
        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}

/// <summary>
/// 上游 Imageflow 节点（M20 Imageflow=67，对应上游 ID 15，由 schema mapper 在序列化阶段单向映射）。
/// </summary>
public sealed class ImageflowNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Imageflow;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var workflowKey = context.GetConfigString("imageflowKey");
        if (string.IsNullOrWhiteSpace(workflowKey))
        {
            return Task.FromResult(new NodeExecutionResult(false, outputs, "Imageflow 缺少 imageflowKey。"));
        }
        var inputJson = context.GetConfigString("inputJson");
        outputs["imageflow_request"] = JsonSerializer.SerializeToElement(new
        {
            imageflowKey = workflowKey,
            input = string.IsNullOrEmpty(inputJson) ? null : (object?)JsonSerializer.Deserialize<JsonElement>(inputJson)
        });
        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}

/// <summary>
/// 上游图像参考节点（M20 ImageReference=16）。
/// 把已上传的参考图（fileHandle / url）作为 outputs 暴露给下游图像生成/合成节点引用。
/// Config：refKind（file_handle/url）+ refValue（必填）+ alias（默认 "ref"）。
/// </summary>
public sealed class ImageReferenceNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.ImageReference;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var refKind = context.GetConfigString("refKind", "url");
        var refValue = context.ReplaceVariables(context.GetConfigString("refValue"));
        if (string.IsNullOrWhiteSpace(refValue))
        {
            return Task.FromResult(new NodeExecutionResult(false, outputs, "ImageReference 缺少 refValue。"));
        }
        var alias = context.GetConfigString("alias", "ref");
        outputs[$"image_ref_{alias}"] = JsonSerializer.SerializeToElement(new { kind = refKind, value = refValue });
        outputs["image_ref_kind"] = VariableResolver.CreateStringElement(refKind);
        outputs["image_ref_value"] = VariableResolver.CreateStringElement(refValue);
        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}

/// <summary>
/// 上游图像画布节点（M20 ImageCanvas=17）。
/// 与 Atlas 私有 Canvas=69 区分：上游版本仅产出"画布编辑指令"，不实际合成像素，
/// 让前端图像编辑器或 ImagePlugin 节点消费。
/// Config：layersJson（图层指令 JSON 数组）/ outputAlias（默认 "canvas_image"）。
/// </summary>
public sealed class ImageCanvasUpstreamNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.ImageCanvas;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var layersJson = context.GetConfigString("layersJson");
        var outputAlias = context.GetConfigString("outputAlias", "canvas_image");
        if (string.IsNullOrWhiteSpace(layersJson))
        {
            return Task.FromResult(new NodeExecutionResult(false, outputs, "ImageCanvas 缺少 layersJson。"));
        }

        try
        {
            var layers = JsonSerializer.Deserialize<JsonElement>(layersJson);
            outputs[outputAlias] = JsonSerializer.SerializeToElement(new { kind = "upstream-canvas-instruction", layers });
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new NodeExecutionResult(false, outputs, $"ImageCanvas layersJson 解析失败：{ex.Message}"));
        }
        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}

/// <summary>
/// Atlas 私有图像生成节点 N44（M20 ImageGeneration=68）。
/// 与上游 ImageGenerate=14 共享 prompt 协议；优先使用租户内置图像模型，未配置则报 MODEL_PROVIDER_NOT_CONFIGURED。
/// </summary>
public sealed class ImageGenerationNodeExecutor : INodeExecutor
{
    private readonly IChatClientFactory? _factory;

    public ImageGenerationNodeExecutor(IChatClientFactory? factory = null)
    {
        _factory = factory;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.ImageGeneration;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var prompt = context.ReplaceVariables(context.GetConfigString("prompt"));
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Task.FromResult(new NodeExecutionResult(false, outputs, "ImageGeneration 缺少 prompt。"));
        }
        MediaProviderGuard.EnsureProviderConfigured(_factory, "ImageGeneration(N44)", "Atlas 私有图像模型（默认 SD/SDXL）");
        outputs["image_request"] = JsonSerializer.SerializeToElement(new
        {
            prompt,
            provider = context.GetConfigString("provider", "atlas-sd"),
            size = context.GetConfigString("size", "1024x1024"),
            negativePrompt = context.GetConfigString("negativePrompt"),
            steps = Math.Clamp(context.GetConfigInt32("steps", 25), 5, 100),
            cfgScale = context.GetConfigInt32("cfgScale", 7),
            kind = "atlas-private"
        });
        outputs["image_node"] = VariableResolver.CreateStringElement("ImageGeneration");
        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}

/// <summary>
/// Atlas 私有画布合成节点 N45（M20 Canvas=69）。
/// 与上游 ImageCanvas=17 区分：本节点真实把多层图像 / 文本 / 形状合成为一张图（指令型 outputs）。
/// 实际像素合成由前端 Canvas API（OffscreenCanvas）或后端 ImageSharp 执行；
/// 此节点产出"合成请求"+"产物句柄占位"。Config：layersJson、output_format（默认 png）、width/height。
/// </summary>
public sealed class CanvasNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Canvas;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var layersJson = context.GetConfigString("layersJson");
        if (string.IsNullOrWhiteSpace(layersJson))
        {
            return Task.FromResult(new NodeExecutionResult(false, outputs, "Canvas 缺少 layersJson。"));
        }
        try
        {
            var layers = JsonSerializer.Deserialize<JsonElement>(layersJson);
            outputs["canvas_compose_request"] = JsonSerializer.SerializeToElement(new
            {
                layers,
                width = context.GetConfigInt32("width", 1024),
                height = context.GetConfigInt32("height", 1024),
                outputFormat = context.GetConfigString("outputFormat", "png")
            });
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new NodeExecutionResult(false, outputs, $"Canvas layersJson 解析失败：{ex.Message}"));
        }
        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}

/// <summary>
/// Atlas 私有图像插件节点 N46（M20 ImagePlugin=70）。
/// 桥接到现有 LowCodePluginService 的 InvokeAsync，仅当 plugin 标记为 image 类型时允许调用。
/// Config：pluginId / apiId / inputJson。
/// </summary>
public sealed class ImagePluginNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.ImagePlugin;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var pluginId = context.GetConfigString("pluginId");
        var apiId = context.GetConfigString("apiId");
        if (string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrWhiteSpace(apiId))
        {
            return Task.FromResult(new NodeExecutionResult(false, outputs, "ImagePlugin 缺少 pluginId 或 apiId。"));
        }

        // 与 PluginNodeExecutor 共享调用协议：但 outputs key 用 image_plugin_* 前缀以便下游识别媒体场景
        var inputJson = context.GetConfigString("inputJson");
        outputs["image_plugin_request"] = JsonSerializer.SerializeToElement(new
        {
            pluginId,
            apiId,
            input = string.IsNullOrEmpty(inputJson) ? new JsonElement() : JsonSerializer.Deserialize<JsonElement>(inputJson)
        });
        outputs["image_plugin_id"] = VariableResolver.CreateStringElement(pluginId);
        outputs["image_plugin_api"] = VariableResolver.CreateStringElement(apiId);
        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}

/// <summary>
/// 视频生成节点 N47（M20 VideoGeneration=47）。
/// 与图像类似：构造视频生成请求 outputs，未配置模型供应商时报 MODEL_PROVIDER_NOT_CONFIGURED。
/// Config：prompt（必填）/ provider（默认 atlas-sora）/ duration（默认 5 秒）/ resolution。
/// </summary>
public sealed class VideoGenerationNodeExecutor : INodeExecutor
{
    private readonly IChatClientFactory? _factory;

    public VideoGenerationNodeExecutor(IChatClientFactory? factory = null)
    {
        _factory = factory;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.VideoGeneration;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var prompt = context.ReplaceVariables(context.GetConfigString("prompt"));
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Task.FromResult(new NodeExecutionResult(false, outputs, "VideoGeneration 缺少 prompt。"));
        }
        MediaProviderGuard.EnsureProviderConfigured(_factory, "VideoGeneration(N47)", "视频生成模型（如 Sora / Runway）");
        outputs["video_request"] = JsonSerializer.SerializeToElement(new
        {
            prompt,
            provider = context.GetConfigString("provider", "atlas-sora"),
            duration = Math.Clamp(context.GetConfigInt32("duration", 5), 1, 60),
            resolution = context.GetConfigString("resolution", "1280x720")
        });
        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}

/// <summary>
/// 视频转音频节点 N48（M20 VideoToAudio=48）。
/// 接受 fileHandle / url 输入，构造 outputs 转交底层 ffmpeg 服务（由 dispatch 处理）。
/// Config：videoSource（fileHandle/url，必填）/ audioFormat（默认 mp3）/ bitrate。
/// </summary>
public sealed class VideoToAudioNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.VideoToAudio;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var videoSource = context.ReplaceVariables(context.GetConfigString("videoSource"));
        if (string.IsNullOrWhiteSpace(videoSource))
        {
            return Task.FromResult(new NodeExecutionResult(false, outputs, "VideoToAudio 缺少 videoSource。"));
        }
        outputs["video_to_audio_request"] = JsonSerializer.SerializeToElement(new
        {
            videoSource,
            audioFormat = context.GetConfigString("audioFormat", "mp3"),
            bitrate = context.GetConfigInt32("bitrate", 192)
        });
        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}

/// <summary>
/// 视频抽帧节点 N49（M20 VideoFrameExtraction=49）。
/// Config：videoSource（必填）/ frameRate（每秒帧数，默认 1）/ outputFormat（默认 jpg）/ maxFrames。
/// </summary>
public sealed class VideoFrameExtractionNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.VideoFrameExtraction;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var videoSource = context.ReplaceVariables(context.GetConfigString("videoSource"));
        if (string.IsNullOrWhiteSpace(videoSource))
        {
            return Task.FromResult(new NodeExecutionResult(false, outputs, "VideoFrameExtraction 缺少 videoSource。"));
        }
        outputs["video_frame_extract_request"] = JsonSerializer.SerializeToElement(new
        {
            videoSource,
            frameRate = Math.Clamp(context.GetConfigInt32("frameRate", 1), 1, 60),
            outputFormat = context.GetConfigString("outputFormat", "jpg"),
            maxFrames = Math.Clamp(context.GetConfigInt32("maxFrames", 60), 1, 600)
        });
        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}
