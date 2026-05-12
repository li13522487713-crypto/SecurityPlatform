namespace Atlas.Infrastructure.Caching;

internal static class HybridCacheSyncHelper
{
    public static void Run(ValueTask task)
    {
        task.AsTask().GetAwaiter().GetResult();
    }

    public static T? Run<T>(ValueTask<T?> task)
    {
        return task.AsTask().GetAwaiter().GetResult();
    }
}
