using System.Text.Json;
using Atlas.Application.Resilience;
using Atlas.Core.Abstractions;
using Atlas.Core.Messaging;
using SqlSugar;

namespace Atlas.Infrastructure.Resilience;

public sealed class InboxService : IInboxService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;

    public InboxService(ISqlSugarClient db, IIdGeneratorAccessor idGen)
    {
        _db = db;
        _idGen = idGen;
    }

    public async Task<bool> HasBeenProcessedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        return await _db.Queryable<InboxMessage>()
            .AnyAsync(x => x.MessageId == messageId && x.IsProcessed, cancellationToken);
    }

    public async Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        await _db.Updateable<InboxMessage>()
            .SetColumns(x => new InboxMessage
            {
                IsProcessed = true,
                ProcessedAt = DateTime.UtcNow
            })
            .Where(x => x.MessageId == messageId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task ProcessOnceAsync<T>(
        Guid messageId,
        string handlerType,
        string payloadJson,
        Func<T, Task> handler,
        CancellationToken cancellationToken)
    {
        if (await HasBeenProcessedAsync(messageId, cancellationToken).ConfigureAwait(false))
            return;

        var claimed = await TryClaimAsync(messageId, handlerType, payloadJson, cancellationToken).ConfigureAwait(false);
        if (!claimed)
            return;

        var payload = JsonSerializer.Deserialize<T>(payloadJson, JsonOptions);
        if (payload is null && default(T) is null && !typeof(T).IsValueType)
            throw new InvalidOperationException("Inbox payload deserialized to null.");

        await handler(payload!).ConfigureAwait(false);
        await MarkProcessedAsync(messageId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> TryClaimAsync(
        Guid messageId,
        string handlerType,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var row = new InboxMessage
        {
            Id = _idGen.NextId(),
            MessageId = messageId,
            HandlerType = handlerType,
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            IsProcessed = false,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await _db.Insertable(row).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (SqlSugarException ex) when (IsUniqueConstraint(ex))
        {
            return false;
        }
    }

    private static bool IsUniqueConstraint(SqlSugarException ex)
    {
        var message = ex.Message ?? string.Empty;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("constraint failed", StringComparison.OrdinalIgnoreCase);
    }
}
