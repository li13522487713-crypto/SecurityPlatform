using System.Reflection;
using System.Text.Json;
using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class BuiltInPluginMetadataProvider
{
    private const string ResourceName = "Atlas.Infrastructure.Resources.BuiltInPlugins.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Lazy<IReadOnlyList<AiPluginBuiltInMeta>> _cache;

    public BuiltInPluginMetadataProvider()
    {
        _cache = new Lazy<IReadOnlyList<AiPluginBuiltInMeta>>(Load, isThreadSafe: true);
    }

    public Task<IReadOnlyList<AiPluginBuiltInMeta>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_cache.Value);
    }

    private static IReadOnlyList<AiPluginBuiltInMeta> Load()
    {
        var assembly = typeof(BuiltInPluginMetadataProvider).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            return [];
        }

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var items = JsonSerializer.Deserialize<List<AiPluginBuiltInMeta>>(json, JsonOptions);
        return items ?? [];
    }
}
