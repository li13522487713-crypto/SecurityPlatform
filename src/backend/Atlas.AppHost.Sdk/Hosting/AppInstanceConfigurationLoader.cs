using System.Text.Json;
using Atlas.Shared.Contracts.Process;
using Microsoft.Extensions.Configuration;

namespace Atlas.AppHost.Sdk.Hosting;

public sealed class AppInstanceConfigurationLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IConfiguration configuration;

    public AppInstanceConfigurationLoader(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public AppInstanceConfig Load()
    {
        var section = configuration.GetSection("AppInstance");
        var fromSection = section.Get<AppInstanceConfig>();
        if (fromSection is not null && !string.IsNullOrWhiteSpace(fromSection.AppKey))
        {
            return fromSection;
        }

        var legacySection = configuration.GetSection("Atlas:AppHost");
        if (legacySection.Exists())
        {
            var legacyAppKey = legacySection["AppKey"]?.Trim();
            if (!string.IsNullOrWhiteSpace(legacyAppKey))
            {
                return new AppInstanceConfig
                {
                    AppKey = legacyAppKey,
                    InstanceId = legacySection["InstanceId"]?.Trim() ?? string.Empty,
                    EnvironmentName = legacySection["EnvironmentName"]?.Trim() ?? "Development",
                    BaseUrl = legacySection["BaseUrl"]?.Trim() ?? string.Empty,
                    LoginUrl = legacySection["LoginUrl"]?.Trim() ?? string.Empty,
                    Port = TryParsePort(legacySection["Port"])
                };
            }
        }

        var filePath = configuration["AppInstance:ConfigPath"];
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            using var stream = File.OpenRead(filePath);
            var fromFile = JsonSerializer.Deserialize<AppInstanceConfig>(stream, SerializerOptions);
            if (fromFile is not null)
            {
                return fromFile;
            }
        }

        return new AppInstanceConfig();
    }

    private static int TryParsePort(string? rawPort)
    {
        return int.TryParse(rawPort, out var parsedPort) && parsedPort > 0
            ? parsedPort
            : 0;
    }
}
