using Atlas.Application.AiPlatform.Models;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IOpenApiPluginParser
{
    Task<OpenApiPluginParseResult> ParseAsync(string openApiJson, CancellationToken cancellationToken);
}
