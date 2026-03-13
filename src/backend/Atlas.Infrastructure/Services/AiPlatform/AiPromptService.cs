using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiPromptService : IAiPromptService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AiPromptTemplateRepository _repository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public AiPromptService(AiPromptTemplateRepository repository, IIdGeneratorAccessor idGeneratorAccessor)
    {
        _repository = repository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<PagedResult<AiPromptTemplateListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        string? category,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(tenantId, keyword, category, pageIndex, pageSize, cancellationToken);
        return new PagedResult<AiPromptTemplateListItem>(
            items.Select(MapListItem).ToList(),
            total,
            pageIndex,
            pageSize);
    }

    public async Task<AiPromptTemplateDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken);
        return entity is null ? null : MapDetail(entity);
    }

    public async Task<long> CreateAsync(TenantId tenantId, AiPromptTemplateCreateRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim();
        if (await _repository.ExistsByNameAsync(tenantId, normalizedName, excludeId: null, cancellationToken))
        {
            throw new BusinessException("Prompt 名称已存在。", ErrorCodes.ValidationError);
        }

        var entity = new AiPromptTemplate(
            tenantId,
            normalizedName,
            request.Description?.Trim(),
            request.Category?.Trim(),
            request.Content,
            SerializeTags(request.Tags),
            request.IsSystem,
            _idGeneratorAccessor.NextId());
        await _repository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long id,
        AiPromptTemplateUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("Prompt 模板不存在。", ErrorCodes.NotFound);
        if (entity.IsSystem)
        {
            throw new BusinessException("系统 Prompt 模板不允许修改。", ErrorCodes.Forbidden);
        }

        var normalizedName = request.Name.Trim();
        if (await _repository.ExistsByNameAsync(tenantId, normalizedName, id, cancellationToken))
        {
            throw new BusinessException("Prompt 名称已存在。", ErrorCodes.ValidationError);
        }

        entity.Update(
            normalizedName,
            request.Description?.Trim(),
            request.Category?.Trim(),
            request.Content,
            SerializeTags(request.Tags));
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("Prompt 模板不存在。", ErrorCodes.NotFound);
        if (entity.IsSystem)
        {
            throw new BusinessException("系统 Prompt 模板不允许删除。", ErrorCodes.Forbidden);
        }

        await _repository.DeleteAsync(tenantId, id, cancellationToken);
    }

    private static string SerializeTags(IReadOnlyList<string>? tags)
    {
        var values = tags?
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        return JsonSerializer.Serialize(values, JsonOptions);
    }

    private static IReadOnlyList<string> ParseTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return [];
        }

        try
        {
            var tags = JsonSerializer.Deserialize<List<string>>(tagsJson, JsonOptions);
            return tags ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static AiPromptTemplateListItem MapListItem(AiPromptTemplate entity)
        => new(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.Category,
            entity.Content,
            ParseTags(entity.TagsJson),
            entity.IsSystem,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static AiPromptTemplateDetail MapDetail(AiPromptTemplate entity)
        => new(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.Category,
            entity.Content,
            ParseTags(entity.TagsJson),
            entity.IsSystem,
            entity.CreatedAt,
            entity.UpdatedAt);
}
