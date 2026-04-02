namespace Atlas.Application.Resilience;

public interface IInboxService
{
    Task<bool> HasBeenProcessedAsync(Guid messageId, CancellationToken cancellationToken);

    Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken);

    Task ProcessOnceAsync<T>(
        Guid messageId,
        string handlerType,
        string payloadJson,
        Func<T, Task> handler,
        CancellationToken cancellationToken);
}
