using Atlas.Domain.Templates;

namespace Atlas.Application.Templates;

public interface IComponentTemplateQueryService
{
    Task<(IReadOnlyList<ComponentTemplate> Items, int Total)> SearchAsync(
        string? keyword,
        TemplateCategory? category,
        string? tags,
        string? version,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ComponentTemplate?> GetByIdAsync(long id, CancellationToken cancellationToken);
}

public interface IComponentTemplateCommandService
{
    Task<long> CreateAsync(CreateTemplateRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(long id, UpdateTemplateRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(long id, CancellationToken cancellationToken);
    /// <summary>从模板实例化，返回模板的 SchemaJson 副本</summary>
    Task<string> InstantiateAsync(long id, CancellationToken cancellationToken);
}

public sealed record CreateTemplateRequest(
    string Name,
    TemplateCategory Category,
    string SchemaJson,
    string Description,
    string Tags,
    string Version);

public sealed record UpdateTemplateRequest(
    string Name,
    string Description,
    string Tags,
    string SchemaJson,
    string Version);
