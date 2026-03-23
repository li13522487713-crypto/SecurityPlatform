using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class OpenApiCallLogService : IOpenApiCallLogService
{
    private readonly ApiCallLogRepository _repository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public OpenApiCallLogService(
        ApiCallLogRepository repository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _repository = repository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task WriteAsync(
        TenantId tenantId,
        OpenApiCallLogCreateRequest request,
        CancellationToken cancellationToken)
    {
        var log = new ApiCallLog(
            tenantId,
            _idGeneratorAccessor.NextId(),
            request.ProjectId,
            request.AppId,
            request.UserId,
            request.ApiName,
            request.HttpMethod,
            request.RequestPath,
            request.IsSuccess,
            request.StatusCode,
            request.ErrorCode,
            request.DurationMs,
            request.TraceId,
            request.CreatedAt);
        await _repository.AddAsync(log, cancellationToken);
    }

    public async Task<OpenApiCallStatsSummary> GetSummaryAsync(
        TenantId tenantId,
        long? projectId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var aggregate = await _repository.AggregateAsync(
            tenantId,
            projectId,
            fromUtc,
            toUtc,
            cancellationToken);
        var failedCalls = aggregate.TotalCalls - aggregate.SuccessCalls;
        var successRate = aggregate.TotalCalls <= 0
            ? 0M
            : Math.Round((decimal)aggregate.SuccessCalls / aggregate.TotalCalls, 4);
        return new OpenApiCallStatsSummary(
            projectId,
            fromUtc,
            toUtc,
            aggregate.TotalCalls,
            aggregate.SuccessCalls,
            failedCalls,
            successRate,
            aggregate.AverageDurationMs,
            aggregate.MaxDurationMs);
    }
}
