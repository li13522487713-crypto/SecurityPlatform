using Atlas.Application.Templates;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Templates;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class ComponentTemplateQueryService : IComponentTemplateQueryService
{
    private readonly ISqlSugarClient _db;
    private readonly ITenantProvider _tenantProvider;

    public ComponentTemplateQueryService(ISqlSugarClient db, ITenantProvider tenantProvider)
    {
        _db = db;
        _tenantProvider = tenantProvider;
    }

    public async Task<(IReadOnlyList<ComponentTemplate> Items, int Total)> SearchAsync(
        string? keyword, TemplateCategory? category, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        var query = _db.Queryable<ComponentTemplate>()
            .Where(t => t.TenantIdValue == tenantId);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(t => t.Name.Contains(keyword) || t.Description.Contains(keyword) || t.Tags.Contains(keyword));
        }

        if (category.HasValue)
        {
            query = query.Where(t => t.Category == category.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(t => t.UpdatedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public async Task<ComponentTemplate?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        return await _db.Queryable<ComponentTemplate>()
            .Where(t => t.Id == id && t.TenantIdValue == tenantId)
            .FirstAsync(cancellationToken);
    }
}

public sealed class ComponentTemplateCommandService : IComponentTemplateCommandService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly ITenantProvider _tenantProvider;

    public ComponentTemplateCommandService(
        ISqlSugarClient db,
        IIdGeneratorAccessor idGen,
        ITenantProvider tenantProvider)
    {
        _db = db;
        _idGen = idGen;
        _tenantProvider = tenantProvider;
    }

    public async Task<long> CreateAsync(CreateTemplateRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var template = new ComponentTemplate
        {
            Id = _idGen.Generator.NextId(),
            TenantId = _tenantProvider.TenantId.Value,
            Name = request.Name,
            Category = request.Category,
            SchemaJson = request.SchemaJson,
            Description = request.Description,
            Tags = request.Tags,
            Version = request.Version,
            IsBuiltIn = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _db.Insertable(template).ExecuteCommandAsync(cancellationToken);
        return template.Id;
    }

    public async Task UpdateAsync(long id, UpdateTemplateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        var rows = await _db.Updateable<ComponentTemplate>()
            .SetColumns(t => new ComponentTemplate
            {
                Name = request.Name,
                Description = request.Description,
                Tags = request.Tags,
                SchemaJson = request.SchemaJson,
                Version = request.Version,
                UpdatedAt = DateTimeOffset.UtcNow
            })
            .Where(t => t.Id == id && t.TenantIdValue == tenantId && !t.IsBuiltIn)
            .ExecuteCommandAsync(cancellationToken);

        if (rows == 0)
        {
            throw new BusinessException("NOT_FOUND", "模板不存在或为内置模板，不可修改");
        }
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        await _db.Deleteable<ComponentTemplate>()
            .Where(t => t.Id == id && t.TenantIdValue == tenantId && !t.IsBuiltIn)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<string> InstantiateAsync(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        var template = await _db.Queryable<ComponentTemplate>()
            .Where(t => t.Id == id && t.TenantIdValue == tenantId)
            .FirstAsync(cancellationToken);

        if (template is null)
        {
            throw new BusinessException("NOT_FOUND", "模板不存在");
        }

        return template.SchemaJson;
    }
}
