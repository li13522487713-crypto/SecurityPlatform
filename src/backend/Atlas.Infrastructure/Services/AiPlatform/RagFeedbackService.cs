using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class RagFeedbackService : IRagFeedbackService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGenerator;

    public RagFeedbackService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        RagFeedbackCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var queryId = request.QueryId.Trim();
        if (string.IsNullOrWhiteSpace(queryId))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "QueryId 不能为空。");
        }

        if (request.Rating is not (1 or -1))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "Rating 仅支持 1 或 -1。");
        }

        var existing = await _db.Queryable<RagFeedback>()
            .FirstAsync(item => item.QueryId == queryId && item.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var entity = new RagFeedback(
            tenantId,
            _idGenerator.NextId(),
            queryId,
            request.Rating,
            request.Comment?.Trim() ?? string.Empty,
            request.ConversationId?.Trim() ?? string.Empty,
            request.AgentId?.Trim() ?? string.Empty,
            userId,
            DateTimeOffset.UtcNow);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<IReadOnlyList<RagFeedbackDto>> GetByQueryIdAsync(
        TenantId tenantId,
        string queryId,
        CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        var normalized = queryId.Trim();
        if (normalized.Length == 0)
        {
            return [];
        }

        var rows = await _db.Queryable<RagFeedback>()
            .Where(item => item.QueryId == normalized)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(item => new RagFeedbackDto(
            item.Id,
            item.QueryId,
            item.Rating,
            item.Comment,
            item.ConversationId,
            item.AgentId,
            item.UserId,
            item.CreatedAt.UtcDateTime)).ToArray();
    }
}
