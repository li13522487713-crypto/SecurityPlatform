using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class KnowledgeQuotaPolicy
{
    private readonly KnowledgeBaseQuotaOptions _options;
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly KnowledgeDocumentRepository _knowledgeDocumentRepository;

    public KnowledgeQuotaPolicy(
        IOptions<KnowledgeBaseQuotaOptions> options,
        KnowledgeBaseRepository knowledgeBaseRepository,
        KnowledgeDocumentRepository knowledgeDocumentRepository)
    {
        _options = options.Value;
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _knowledgeDocumentRepository = knowledgeDocumentRepository;
    }

    public async Task EnsureCanCreateKnowledgeBaseAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var count = await _knowledgeBaseRepository.CountByTenantAsync(tenantId, cancellationToken);
        if (count >= _options.MaxPerTenant)
        {
            throw new BusinessException(
                $"租户知识库数量已达上限（{_options.MaxPerTenant}）。",
                ErrorCodes.ValidationError);
        }
    }

    public async Task EnsureCanAddDocumentAsync(TenantId tenantId, long knowledgeBaseId, CancellationToken cancellationToken)
    {
        var count = await _knowledgeDocumentRepository.CountByKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken);
        if (count >= _options.MaxDocsPerKnowledgeBase)
        {
            throw new BusinessException(
                $"该知识库文档数已达上限（{_options.MaxDocsPerKnowledgeBase}）。",
                ErrorCodes.ValidationError);
        }
    }

    public void EnsureFileSize(long fileSizeBytes)
    {
        if (fileSizeBytes > _options.MaxFileSizeBytes)
        {
            throw new BusinessException(
                $"文件超过大小上限（{_options.MaxFileSizeMB} MB）。",
                ErrorCodes.ValidationError);
        }
    }
}
