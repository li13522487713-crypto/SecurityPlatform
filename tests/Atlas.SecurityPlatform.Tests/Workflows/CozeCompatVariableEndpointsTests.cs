using System.Text.Json;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Presentation.Shared.Controllers.Ai;

namespace Atlas.SecurityPlatform.Tests.Workflows;

/// <summary>
/// M1：Coze 兼容层新增端点的 DTO/Request 协议契约测试。
///
/// 由于 <see cref="CozeWorkflowCompatControllerBase"/> 真实构造依赖大量仓储与执行服务，
/// 本测试只覆盖：
/// 1) 新端点请求 record 能从 Coze Thrift 风格的 snake_case JSON 反序列化；
/// 2) 新增 DTO 在 JSON Web 序列化下字段命名/类型符合预期；
/// 3) 变量树作用域枚举的整数值不被意外修改（前端依赖该枚举做分组）。
/// </summary>
public sealed class CozeCompatVariableEndpointsTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CozeValidateTreeRequest_ShouldRoundtripFromUpstreamPayload()
    {
        var json = """
        {
          "workflow_id": "100",
          "bind_project_id": "proj-1",
          "schema": "{\"nodes\":[],\"connections\":[]}"
        }
        """;

        var request = JsonSerializer.Deserialize<CozeValidateTreeRequest>(json, WebJson);

        Assert.NotNull(request);
        Assert.Equal("100", request!.workflow_id);
        Assert.Equal("proj-1", request.bind_project_id);
        Assert.Contains("nodes", request.schema);
    }

    [Fact]
    public void CozeNodePanelSearchRequest_ShouldRoundtripPaginationFields()
    {
        var json = """
        {
          "search_type": 0,
          "space_id": "atlas-workflow",
          "search_key": "Llm",
          "page_or_cursor": "2",
          "page_size": 10,
          "exclude_workflow_id": "999"
        }
        """;

        var request = JsonSerializer.Deserialize<CozeNodePanelSearchRequest>(json, WebJson);

        Assert.NotNull(request);
        Assert.Equal(0, request!.search_type);
        Assert.Equal("Llm", request.search_key);
        Assert.Equal("2", request.page_or_cursor);
        Assert.Equal(10, request.page_size);
        Assert.Equal("999", request.exclude_workflow_id);
    }

    [Fact]
    public void CozeGetHistorySchemaRequest_ShouldRoundtripVersionFields()
    {
        var json = """
        {
          "workflow_id": "100",
          "commit_id": "v1",
          "execute_id": "200",
          "log_id": "log-1"
        }
        """;

        var request = JsonSerializer.Deserialize<CozeGetHistorySchemaRequest>(json, WebJson);

        Assert.NotNull(request);
        Assert.Equal("v1", request!.commit_id);
        Assert.Equal("200", request.execute_id);
        Assert.Equal("log-1", request.log_id);
    }

    [Fact]
    public void CozeGetNodeExecuteHistoryRequest_ShouldRoundtripBatchFields()
    {
        var json = """
        {
          "workflow_id": "100",
          "space_id": "atlas-workflow",
          "execute_id": "200",
          "node_id": "llm_1",
          "is_batch": true,
          "batch_index": 3,
          "node_type": "Llm"
        }
        """;

        var request = JsonSerializer.Deserialize<CozeGetNodeExecuteHistoryRequest>(json, WebJson);

        Assert.NotNull(request);
        Assert.True(request!.is_batch);
        Assert.Equal(3, request.batch_index);
        Assert.Equal("Llm", request.node_type);
    }

    [Fact]
    public void WorkflowNodeExecutionHistoryDto_ShouldSerializeAllFields()
    {
        var dto = new WorkflowNodeExecutionHistoryDto(
            WorkflowId: "100",
            ExecutionId: "200",
            NodeKey: "llm_1",
            NodeType: WorkflowNodeType.Llm.ToString(),
            Status: ExecutionStatus.Completed,
            InputJson: "{\"prompt\":\"hi\"}",
            OutputJson: "{\"text\":\"hello\"}",
            ContextVariablesJson: "{\"entry_1.value\":\"42\"}",
            ErrorMessage: null,
            StartedAt: DateTime.UtcNow,
            CompletedAt: DateTime.UtcNow.AddSeconds(2),
            DurationMs: 2000);

        var json = JsonSerializer.Serialize(dto, WebJson);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("workflowId", out var workflowId));
        Assert.Equal("100", workflowId.GetString());
        Assert.True(doc.RootElement.TryGetProperty("contextVariablesJson", out var ctxVars));
        Assert.Contains("entry_1.value", ctxVars.GetString());
        Assert.True(doc.RootElement.TryGetProperty("status", out var status));
        Assert.Equal((int)ExecutionStatus.Completed, status.GetInt32());
    }

    [Fact]
    public void WorkflowHistorySchemaDto_ShouldExposeCanvasJsonAndCommit()
    {
        var dto = new WorkflowHistorySchemaDto(
            WorkflowId: "100",
            CommitId: "abc",
            SchemaJson: "{\"nodes\":[]}",
            Name: "Atlas Workflow",
            Description: "demo",
            SnapshotAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var json = JsonSerializer.Serialize(dto, WebJson);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("100", doc.RootElement.GetProperty("workflowId").GetString());
        Assert.Equal("abc", doc.RootElement.GetProperty("commitId").GetString());
        Assert.Equal("{\"nodes\":[]}", doc.RootElement.GetProperty("schemaJson").GetString());
        Assert.Equal("Atlas Workflow", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void WorkflowVariableTreeDto_ShouldGroupByScopeKind()
    {
        var systemGroup = new WorkflowVariableGroup(
            Scope: WorkflowVariableScopeKind.System,
            GroupKey: "system",
            GroupName: "System",
            SourceNodeKey: null,
            SourceNodeType: null,
            Fields: new[]
            {
                new WorkflowVariableField("user_id", "user_id", "string", "当前登录用户")
            });

        var nodeGroup = new WorkflowVariableGroup(
            Scope: WorkflowVariableScopeKind.Node,
            GroupKey: "entry_1",
            GroupName: "Entry",
            SourceNodeKey: "entry_1",
            SourceNodeType: "Entry",
            Fields: new[]
            {
                new WorkflowVariableField("output", "output", "string")
            });

        var dto = new WorkflowVariableTreeDto("100", "llm_1", new[] { systemGroup, nodeGroup });
        var json = JsonSerializer.Serialize(dto, WebJson);
        using var doc = JsonDocument.Parse(json);
        var groups = doc.RootElement.GetProperty("groups");

        Assert.Equal(2, groups.GetArrayLength());
        Assert.Equal((int)WorkflowVariableScopeKind.System, groups[0].GetProperty("scope").GetInt32());
        Assert.Equal((int)WorkflowVariableScopeKind.Node, groups[1].GetProperty("scope").GetInt32());
        Assert.Equal("entry_1", groups[1].GetProperty("sourceNodeKey").GetString());
    }

    [Theory]
    [InlineData(WorkflowVariableScopeKind.Node, 0)]
    [InlineData(WorkflowVariableScopeKind.Global, 1)]
    [InlineData(WorkflowVariableScopeKind.System, 2)]
    [InlineData(WorkflowVariableScopeKind.Conversation, 3)]
    [InlineData(WorkflowVariableScopeKind.User, 4)]
    public void WorkflowVariableScopeKind_ShouldHaveStableNumericValues(WorkflowVariableScopeKind scope, int expected)
    {
        Assert.Equal(expected, (int)scope);
    }
}
