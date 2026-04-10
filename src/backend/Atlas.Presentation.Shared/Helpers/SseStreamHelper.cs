using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;

namespace Atlas.Presentation.Shared.Helpers;

public static class SseStreamHelper
{
    public static bool ShouldUseStructuredEvents(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Stream-Event-Mode", out var mode) &&
            mode.Count > 0 &&
            string.Equals(mode[0], "react", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (request.Query.TryGetValue("eventMode", out var eventMode) &&
            eventMode.Count > 0 &&
            string.Equals(eventMode[0], "react", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public static async IAsyncEnumerable<string> AppendDone(
        IAsyncEnumerable<string> source,
        string doneToken = "[DONE]",
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            yield return item;
        }

        yield return doneToken;
    }

    public static async IAsyncEnumerable<SseItem<string>> AppendDone(
        IAsyncEnumerable<SseItem<string>> source,
        string doneToken = "[DONE]",
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            yield return item;
        }

        yield return new SseItem<string>(doneToken);
    }

    public static async IAsyncEnumerable<SseItem<string>> ToSseItems<TEvent>(
        IAsyncEnumerable<TEvent> source,
        Func<TEvent, string> eventTypeSelector,
        Func<TEvent, string> dataSelector,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var evt in source.WithCancellation(cancellationToken))
        {
            yield return new SseItem<string>(dataSelector(evt), eventTypeSelector(evt));
        }
    }
}
