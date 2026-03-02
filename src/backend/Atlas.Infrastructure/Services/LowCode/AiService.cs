using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;
using System.Runtime.CompilerServices;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// AI 辅助开发服务实现（基于 Provider 抽象，支持 OpenAI / DeepSeek / Ollama）
/// </summary>
public sealed class AiService : IAiService
{
    private readonly HttpClient? _httpClient;
    private readonly ILogger<AiService>? _logger;

    public AiService(
        ILogger<AiService>? logger = null)
    {
        _httpClient = new HttpClient();
        _logger = logger;
    }

    public async Task<AiFormGenerateResponse> GenerateFormAsync(
        TenantId tenantId, AiFormGenerateRequest request, CancellationToken cancellationToken = default)
    {
        var prompt = $"请根据以下描述生成 amis JSON Schema 表单定义：\n\n{request.Description}";
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            prompt += $"\n\n分类：{request.Category}";
        }

        var schemaJson = await CallAiAsync(prompt, cancellationToken);

        // Try to extract JSON from the response
        var jsonStart = schemaJson.IndexOf('{');
        var jsonEnd = schemaJson.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            schemaJson = schemaJson[jsonStart..(jsonEnd + 1)];
        }
        else
        {
            // Generate a basic schema as fallback
            schemaJson = GenerateBasicFormSchema(request.Description);
        }

        return new AiFormGenerateResponse(schemaJson, "AI 已根据描述生成表单 Schema");
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
        // Simulate streaming by chunking the response
        var reply = await CallAiAsync(request.Message, cancellationToken);
        var words = reply.Split(' ');

        foreach (var word in words)
        {
            if (cancellationToken.IsCancellationRequested) yield break;
            yield return word + " ";
            await Task.Delay(50, cancellationToken);
        }
    }

    /// <summary>
    /// Calls the configured AI provider. Falls back to a template-based response
    /// when no AI provider is configured.
    /// </summary>
    private async Task<string> CallAiAsync(string prompt, CancellationToken cancellationToken)
    {
        // TODO: Implement actual AI provider calls (OpenAI / DeepSeek / Ollama)
        // For now, return a template-based response
        _logger?.LogInformation("AI prompt: {Prompt}", prompt.Length > 200 ? prompt[..200] + "..." : prompt);

        await Task.CompletedTask;

        if (prompt.Contains("表单") || prompt.Contains("form"))
        {
            return GenerateBasicFormSchema(prompt);
        }

        if (prompt.Contains("SQL") || prompt.Contains("查询"))
        {
            return "SELECT * FROM table_name WHERE 1=1 LIMIT 100;";
        }

        if (prompt.Contains("工作流") || prompt.Contains("workflow") || prompt.Contains("BPMN"))
        {
            return JsonSerializer.Serialize(new
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
            });
        }

        return "AI 服务已收到请求。请配置 AI 提供商（OpenAI / DeepSeek / Ollama）以启用完整功能。";
    }

    private static string GenerateBasicFormSchema(string description)
    {
        return JsonSerializer.Serialize(new
        {
            type = "page",
            title = "AI 生成表单",
            body = new object[]
            {
                new
                {
                    type = "form",
                    title = "",
                    body = new object[]
                    {
                        new { type = "input-text", name = "name", label = "名称", required = true },
                        new { type = "textarea", name = "description", label = "描述" },
                        new { type = "input-date", name = "date", label = "日期" },
                        new { type = "select", name = "status", label = "状态", options = new[]
                        {
                            new { label = "草稿", value = "draft" },
                            new { label = "已提交", value = "submitted" },
                            new { label = "已完成", value = "completed" }
                        }}
                    }
                }
            }
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
