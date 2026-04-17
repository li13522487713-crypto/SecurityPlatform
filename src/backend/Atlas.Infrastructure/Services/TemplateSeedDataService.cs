using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Templates;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class TemplateSeedDataService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public TemplateSeedDataService(
        ISqlSugarClient db,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _db = db;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task InitializeBuiltInTemplatesAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var seeds = new[]
        {
            new
            {
                Name = "客户录入表单",
                Category = TemplateCategory.Form,
                Description = "标准客户录入表单模板",
                Tags = "crm,form,customer",
                Version = "1.0.0",
                SchemaJson = "{\"schemaVersion\":1,\"kind\":\"form\",\"title\":\"客户录入\",\"fields\":[{\"key\":\"name\",\"label\":\"客户名称\",\"control\":\"text\"},{\"key\":\"mobile\",\"label\":\"手机号\",\"control\":\"text\"}]}"
            },
            new
            {
                Name = "审批流程基础模板",
                Category = TemplateCategory.Flow,
                Description = "包含开始、审批、结束节点的流程模板",
                Tags = "approval,workflow,starter",
                Version = "1.0.0",
                SchemaJson = "{\"id\":\"approval-basic\",\"version\":1,\"steps\":[{\"id\":\"step1\",\"name\":\"开始\",\"stepType\":\"Sequence\"}]}"
            },
            new
            {
                Name = "数据列表页模板",
                Category = TemplateCategory.Page,
                Description = "通用列表查询页面模板",
                Tags = "list,page,table",
                Version = "1.0.0",
                SchemaJson = "{\"schemaVersion\":1,\"kind\":\"page\",\"title\":\"数据列表\",\"layout\":{\"kind\":\"dataTable\",\"queryEndpoint\":\"/api/v1/dynamic-tables/sample/records/query\"}}"
            }
        };

        var seedNames = seeds.Select(x => x.Name).ToArray();
        var existing = await _db.Queryable<ComponentTemplate>()
            .Where(t => t.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(seedNames, t.Name))
            .ToListAsync(cancellationToken);
        var existingSet = existing.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toInsert = new List<ComponentTemplate>();
        foreach (var seed in seeds)
        {
            if (existingSet.Contains(seed.Name))
            {
                continue;
            }

            var entity = new ComponentTemplate(tenantId, _idGeneratorAccessor.NextId())
            {
                Name = seed.Name,
                Category = seed.Category,
                Description = seed.Description,
                Tags = seed.Tags,
                Version = seed.Version,
                SchemaJson = seed.SchemaJson,
                IsBuiltIn = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            toInsert.Add(entity);
        }

        if (toInsert.Count > 0)
        {
            await _db.Insertable(toInsert).ExecuteCommandAsync(cancellationToken);
        }
    }
}
