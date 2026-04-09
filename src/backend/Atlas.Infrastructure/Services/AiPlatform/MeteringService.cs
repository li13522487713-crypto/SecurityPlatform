using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class MeteringService : IMeteringService
{
    private readonly LlmUsageRecordRepository _llmUsageRecordRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public MeteringService(
        LlmUsageRecordRepository llmUsageRecordRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _llmUsageRecordRepository = llmUsageRecordRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task RecordLlmUsageAsync(
        TenantId tenantId,
        LlmUsageRecordCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantId.IsEmpty || request.TotalTokens <= 0)
        {
            return;
        }

        var entity = new LlmUsageRecord(
            tenantId,
            _idGeneratorAccessor.NextId(),
            request.Provider,
            request.Model,
            request.Source,
            request.PromptTokens,
            request.CompletionTokens,
            request.TotalTokens,
            request.EstimatedCostUsd);
        await _llmUsageRecordRepository.AddAsync(entity, cancellationToken);
    }
}
