namespace Atlas.Application.Plugins.Models;

public sealed record PluginDescriptor(
    string Code,
    string Name,
    string Version,
    string AssemblyName,
    string FilePath,
    string State,
    DateTimeOffset LoadedAt,
    string? ErrorMessage);
