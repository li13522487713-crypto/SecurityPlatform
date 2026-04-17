namespace Atlas.Application.LowCode.Models;

/// <summary>
/// AI 辅助开发请求与响应模型
/// </summary>

public sealed record AiSqlGenerateRequest(string Question, string? TableContext);
public sealed record AiSqlGenerateResponse(string Sql, string? Explanation);

public sealed record AiWorkflowSuggestRequest(string Description);
public sealed record AiWorkflowSuggestResponse(string DefinitionJson, string? Explanation);

public sealed record AiChatRequest(string Message, string? Context);
public sealed record AiChatResponse(string Reply);

public sealed record AiProviderConfig(string Provider, string ApiKey, string? BaseUrl, string? Model, int? MaxTokens);
