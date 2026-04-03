using Atlas.Shared.Contracts.Health;
using Atlas.Shared.Contracts.Process;

namespace Atlas.AppHost.Sdk.Health;

public static class AppHealthResponseBuilder
{
    public static AppHealthReport BuildLive(string version) =>
        new()
        {
            Status = AppProcessStatus.Running,
            Live = true,
            Ready = true,
            Version = version,
            Message = "AppHost is live."
        };

    public static AppHealthReport BuildReady(string version, string? message = null) =>
        new()
        {
            Status = AppProcessStatus.Running,
            Live = true,
            Ready = true,
            Version = version,
            Message = string.IsNullOrWhiteSpace(message) ? "AppHost is ready." : message
        };
}
