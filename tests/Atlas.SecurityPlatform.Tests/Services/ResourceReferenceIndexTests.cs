using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Services.LowCode;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class ResourceReferenceIndexTests
{
    private sealed class CapturingGuard : IResourceReferenceGuardService
    {
        public IReadOnlyList<AppResourceReferenceDto> Captured { get; private set; } = Array.Empty<AppResourceReferenceDto>();
        public Task EnsureCanDeleteAsync(TenantId tenantId, string resourceType, string resourceId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<AppResourceReferenceDto>> ListByResourceAsync(TenantId tenantId, string resourceType, string resourceId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AppResourceReferenceDto>>(Array.Empty<AppResourceReferenceDto>());
        public Task ReindexForAppAsync(TenantId tenantId, long appId, IReadOnlyList<AppResourceReferenceDto> references, CancellationToken cancellationToken)
        {
            Captured = references;
            return Task.CompletedTask;
        }
    }

    private static (ResourceReferenceIndex Idx, CapturingGuard Guard) Create()
    {
        var guard = new CapturingGuard();
        return (new ResourceReferenceIndex(guard, NullLogger<ResourceReferenceIndex>.Instance), guard);
    }

    [Fact]
    public async Task Empty_Json_Clears_Index()
    {
        var (idx, guard) = Create();
        await idx.ReindexFromSchemaJsonAsync(new TenantId(Guid.NewGuid()), 1, "", CancellationToken.None);
        Assert.Empty(guard.Captured);
    }

    [Fact]
    public async Task Extracts_Workflow_Chatflow_Plugin_PromptTemplate_Refs()
    {
        var schema = """
        {
          "appId": "demo",
          "pages": [{
            "id": "home",
            "root": {
              "id": "btn-1",
              "events": [{ "name": "onClick", "actions": [
                { "kind": "call_workflow", "payload": { "workflowId": "9999" } },
                { "kind": "call_chatflow", "payload": { "chatflowId": "5555" } }
              ]}],
              "props": {
                "pluginId": "plg_1",
                "promptTemplateId": "tpl_a",
                "knowledgeBaseId": "kb_1",
                "databaseInfoId": "db_1",
                "triggerId": "trg_1"
              }
            }
          }]
        }
        """;
        var (idx, guard) = Create();
        await idx.ReindexFromSchemaJsonAsync(new TenantId(Guid.NewGuid()), 100, schema, CancellationToken.None);
        var byType = guard.Captured.GroupBy(r => r.ResourceType).ToDictionary(g => g.Key, g => g.Select(r => r.ResourceId).ToHashSet());
        Assert.Contains("9999", byType["workflow"]);
        Assert.Contains("5555", byType["chatflow"]);
        Assert.Contains("plg_1", byType["plugin"]);
        Assert.Contains("tpl_a", byType["prompt-template"]);
        Assert.Contains("kb_1", byType["knowledge"]);
        Assert.Contains("db_1", byType["database"]);
        Assert.Contains("trg_1", byType["trigger"]);
    }

    [Fact]
    public async Task Extracts_Variable_Path_From_Binding()
    {
        var schema = """
        {
          "pages": [{
            "root": {
              "props": {
                "value": { "sourceType": "variable", "valueType": "string", "path": "page.formValues" },
                "title": { "sourceType": "variable", "valueType": "string", "path": "app.currentUser" }
              }
            }
          }]
        }
        """;
        var (idx, guard) = Create();
        await idx.ReindexFromSchemaJsonAsync(new TenantId(Guid.NewGuid()), 1, schema, CancellationToken.None);
        var vars = guard.Captured.Where(r => r.ResourceType == "variable").Select(r => r.ResourceId).ToHashSet();
        Assert.Contains("page.formValues", vars);
        Assert.Contains("app.currentUser", vars);
    }

    [Fact]
    public async Task Invalid_Json_Does_Not_Clear_Index()
    {
        var (idx, guard) = Create();
        // 先用合法 schema 索引
        await idx.ReindexFromSchemaJsonAsync(new TenantId(Guid.NewGuid()), 1, """{"props":{"workflowId":"123"}}""", CancellationToken.None);
        var before = guard.Captured.Count;
        // 再用非法 JSON
        await idx.ReindexFromSchemaJsonAsync(new TenantId(Guid.NewGuid()), 1, "{not-json}", CancellationToken.None);
        // 解析失败时不应清空（保留上次成功索引）
        Assert.Equal(before, guard.Captured.Count);
    }

    [Fact]
    public async Task Deduplicates_Same_Reference_Path()
    {
        // 同一个 workflowId 出现两次同 path 时去重；不同 path 则保留。
        var schema = """
        {
          "props": { "workflowId": "1" },
          "child": { "workflowId": "1" }
        }
        """;
        var (idx, guard) = Create();
        await idx.ReindexFromSchemaJsonAsync(new TenantId(Guid.NewGuid()), 1, schema, CancellationToken.None);
        var paths = guard.Captured
            .Where(r => r.ResourceType == "workflow" && r.ResourceId == "1")
            .Select(r => r.ReferencePath)
            .Distinct()
            .ToList();
        Assert.Equal(2, paths.Count); // 两条不同 path 都保留
    }
}
