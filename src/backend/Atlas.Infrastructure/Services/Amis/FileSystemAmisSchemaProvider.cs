using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.Amis.Abstractions;
using Atlas.Application.Amis.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.Amis;

public sealed class FileSystemAmisSchemaProvider : IAmisSchemaProvider
{
    private readonly string _schemaDirectory;
    private readonly string _schemaDirectoryFullPath;
    private readonly ILogger<FileSystemAmisSchemaProvider> _logger;
    private readonly ConcurrentDictionary<string, AmisPageDefinition> _cache = new(StringComparer.OrdinalIgnoreCase);

    public FileSystemAmisSchemaProvider(IHostEnvironment environment, ILogger<FileSystemAmisSchemaProvider> logger)
    {
        _logger = logger;
        _schemaDirectory = Path.Combine(environment.ContentRootPath, "AmisSchemas");
        _schemaDirectoryFullPath = Path.GetFullPath(_schemaDirectory);
    }

    public async Task<AmisPageDefinition?> GetByKeyAsync(string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var filePath = Path.Combine(_schemaDirectory, $"{key}.json");
        var fullPath = Path.GetFullPath(filePath);
        var rootWithSeparator = _schemaDirectoryFullPath.EndsWith(Path.DirectorySeparatorChar)
            ? _schemaDirectoryFullPath
            : _schemaDirectoryFullPath + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("AMIS schema 路径越界访问被拒绝: {Key}", key);
            return null;
        }

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("AMIS schema 文件未找到: {Key}", key);
            return null;
        }

        try
        {
            var text = await File.ReadAllTextAsync(fullPath, cancellationToken);
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (!root.TryGetProperty("schema", out var schemaElement))
            {
                _logger.LogWarning("AMIS schema {Key} 缺少 schema 节点", key);
                return null;
            }

            var title = root.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null;
            var tableKey = root.TryGetProperty("tableKey", out var tableKeyElement) ? tableKeyElement.GetString() : null;
            var description = root.TryGetProperty("description", out var descElement) ? descElement.GetString() : null;
            var schemaClone = schemaElement.Clone();

            var definition = new AmisPageDefinition(
                key,
                title ?? key,
                tableKey ?? key,
                description,
                schemaClone);

            _cache[key] = definition;
            return definition;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "解析 AMIS schema {Key} 失败", key);
            return null;
        }
    }
}
