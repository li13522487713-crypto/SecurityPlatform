using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

/// <summary>
/// AI 辅助开发服务抽象
/// </summary>
public interface IAiService
{
    Task<AiFormGenerateResponse> GenerateFormAsync(TenantId tenantId, AiFormGenerateRequest request, CancellationToken cancellationToken = default);
    Task<AiSqlGenerateResponse> GenerateSqlAsync(TenantId tenantId, AiSqlGenerateRequest request, CancellationToken cancellationToken = default);
    Task<AiWorkflowSuggestResponse> SuggestWorkflowAsync(TenantId tenantId, AiWorkflowSuggestRequest request, CancellationToken cancellationToken = default);
    Task<AiChatResponse> ChatAsync(TenantId tenantId, AiChatRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> ChatStreamAsync(TenantId tenantId, AiChatRequest request, CancellationToken cancellationToken = default);
}
