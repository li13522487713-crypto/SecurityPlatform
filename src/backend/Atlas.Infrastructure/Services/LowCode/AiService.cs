using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// AI 辅助开发服务实现（基于 Provider 抽象，支持 OpenAI / DeepSeek / Ollama）
/// </summary>
public sealed class AiService : IAiService
{
    private readonly ILlmProviderFactory _llmProviderFactory;
    private readonly ILogger<AiService> _logger;
    private readonly AiPlatformOptions _aiOptions;

    public AiService(
        ILlmProviderFactory llmProviderFactory,
        IOptions<AiPlatformOptions> aiOptions,
        ILogger<AiService> logger)
    {
        _llmProviderFactory = llmProviderFactory;
        _logger = logger;
        _aiOptions = aiOptions.Value;
    }

    public async Task<AiSqlGenerateResponse> GenerateSqlAsync(
        TenantId tenantId, AiSqlGenerateRequest request, CancellationToken cancellationToken = default)
    {
        var prompt = $"请将以下自然语言查询转换为 SQL：\n\n{request.Question}";
        if (!string.IsNullOrWhiteSpace(request.TableContext))
        {
            prompt += $"\n\n数据库表结构：\n{request.TableContext}";
        }

        var sql = await CallAiAsync(prompt, cancellationToken);
        return new AiSqlGenerateResponse(sql, "AI 已根据描述生成 SQL");
    }

    public async Task<AiWorkflowSuggestResponse> SuggestWorkflowAsync(
        TenantId tenantId, AiWorkflowSuggestRequest request, CancellationToken cancellationToken = default)
    {
        var prompt = $"请根据以下业务流程描述生成 BPMN 工作流定义 JSON：\n\n{request.Description}";
        var definitionJson = await CallAiAsync(prompt, cancellationToken);
        return new AiWorkflowSuggestResponse(definitionJson, "AI 已根据描述建议工作流");
    }

    public async Task<AiChatResponse> ChatAsync(
        TenantId tenantId, AiChatRequest request, CancellationToken cancellationToken = default)
    {
        var prompt = request.Message;
        if (!string.IsNullOrWhiteSpace(request.Context))
        {
            prompt = $"上下文：{request.Context}\n\n用户问题：{request.Message}";
        }

        var reply = await CallAiAsync(prompt, cancellationToken);
        return new AiChatResponse(reply);
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        TenantId tenantId, AiChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var prompt = request.Message;
        if (!string.IsNullOrWhiteSpace(request.Context))
        {
            prompt = $"上下文：{request.Context}\n\n用户问题：{request.Message}";
        }

        var requestModel = BuildChatRequest(prompt);
        ChatCompletionResult? fallbackResult = null;
        IAsyncEnumerable<ChatCompletionChunk>? stream = null;

        try
        {
            var provider = _llmProviderFactory.GetLlmProvider(requestModel.Provider);
            stream = provider.ChatStreamAsync(requestModel, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI stream provider unavailable, fallback to template mode.");
            fallbackResult = await GetTemplateFallbackAsync(prompt);
        }

        if (stream is not null)
        {
            await using var streamEnumerator = stream.GetAsyncEnumerator(cancellationToken);
            while (true)
            {
                ChatCompletionChunk chunk;
                try
                {
                    if (!await streamEnumerator.MoveNextAsync())
                    {
                        break;
                    }

                    chunk = streamEnumerator.Current;
                }

                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AI stream execution failed, fallback to template mode.");
                    fallbackResult = await GetTemplateFallbackAsync(prompt);
                    break;
                }

                if (!string.IsNullOrWhiteSpace(chunk.ContentDelta))
                {
                    yield return chunk.ContentDelta;
                }
            }
        }

        if (fallbackResult is not null)
        {
            yield return fallbackResult.Content;
        }
    }

    /// <summary>
    /// Calls the configured AI provider. Falls back to a template-based response
    /// when no AI provider is configured.
    /// </summary>
    private async Task<string> CallAiAsync(string prompt, CancellationToken cancellationToken)
    {
        _logger.LogInformation("AI prompt: {Prompt}", prompt.Length > 200 ? prompt[..200] + "..." : prompt);

        try
        {
            var request = BuildChatRequest(prompt);
            var provider = _llmProviderFactory.GetLlmProvider(request.Provider);
            var result = await provider.ChatAsync(request, cancellationToken);
            return result.Content;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI provider call failed. Fallback to template response.");
            var fallback = await GetTemplateFallbackAsync(prompt);
            return fallback.Content;
        }
    }

    private ChatCompletionRequest BuildChatRequest(string prompt)
    {
        var provider = _aiOptions.DefaultProvider;
        var model = _aiOptions.Providers.TryGetValue(provider, out var providerOption)
                    && !string.IsNullOrWhiteSpace(providerOption.DefaultModel)
            ? providerOption.DefaultModel
            : "gpt-4o-mini";

        return new ChatCompletionRequest(
            model,
            [new ChatMessage("user", prompt)],
            Temperature: 0.2f,
            MaxTokens: 2048,
            Provider: provider);
    }

    private async Task<ChatCompletionResult> GetTemplateFallbackAsync(string prompt)
    {
        await Task.CompletedTask;

        if (prompt.Contains("SQL", StringComparison.OrdinalIgnoreCase) || prompt.Contains("查询", StringComparison.OrdinalIgnoreCase))
        {
            return new ChatCompletionResult("SELECT * FROM table_name WHERE 1=1 LIMIT 100;", Provider: "fallback");
        }

        if (prompt.Contains("工作流", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("workflow", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("BPMN", StringComparison.OrdinalIgnoreCase))
        {
            return new ChatCompletionResult(
                JsonSerializer.Serialize(new
                {
                    nodes = new[]
                    {
                        new { id = "start", type = "StartEvent", name = "开始" },
                        new { id = "task1", type = "UserTask", name = "审批节点" },
                        new { id = "end", type = "EndEvent", name = "结束" }
                    },
                    edges = new[]
                    {
                        new { source = "start", target = "task1" },
                        new { source = "task1", target = "end" }
                    }
                }),
                Provider: "fallback");
        }

        return new ChatCompletionResult(
            "AI 服务已收到请求。请配置 AI 提供商（OpenAI / DeepSeek / Ollama）以启用完整功能。",
            Provider: "fallback");
    }
}
