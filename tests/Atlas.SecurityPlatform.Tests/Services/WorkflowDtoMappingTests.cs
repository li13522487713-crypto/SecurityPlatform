using System.Text.Json;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.SecurityPlatform.Tests.Services;

/// <summary>
/// TS-22: WorkflowV2 DTO 映射测试。
/// 验证请求/响应 DTO 的构造、序列化、字段完整性。
/// </summary>
public sealed class WorkflowDtoMappingTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // ─── Request DTOs ─────────────────────────────────────────────────────────

    [Fact]
    public void WorkflowV2CreateRequest_ShouldRoundtripViaJson()
    {
        var req = new WorkflowV2CreateRequest("Test Flow", "A test", WorkflowMode.Standard);
        var json = JsonSerializer.Serialize(req, JsonOpts);
        var restored = JsonSerializer.Deserialize<WorkflowV2CreateRequest>(json, JsonOpts);

        Assert.NotNull(restored);
        Assert.Equal(req.Name, restored.Name);
        Assert.Equal(req.Description, restored.Description);
        Assert.Equal(req.Mode, restored.Mode);
    }

    [Fact]
    public void WorkflowV2SaveDraftRequest_ShouldRoundtripViaJson()
    {
        const string canvas = """{"nodes":[],"connections":[]}""";
        var req = new WorkflowV2SaveDraftRequest(canvas, "commit-abc");
        var json = JsonSerializer.Serialize(req, JsonOpts);
        var restored = JsonSerializer.Deserialize<WorkflowV2SaveDraftRequest>(json, JsonOpts);

        Assert.NotNull(restored);
        Assert.Equal(canvas, restored.CanvasJson);
        Assert.Equal("commit-abc", restored.CommitId);
    }

    [Fact]
    public void WorkflowV2RunRequest_DefaultsSource_ShouldBeNull()
    {
        var req = new WorkflowV2RunRequest("{}", null);
        Assert.Null(req.Source);
        Assert.Equal("{}", req.InputsJson);
    }

    // ─── Response DTOs ────────────────────────────────────────────────────────

    [Fact]
    public void WorkflowV2ListItem_ShouldSerializeAllFields()
    {
        var item = new WorkflowV2ListItem(
            Id: 100L,
            Name: "My Workflow",
            Description: "desc",
            Mode: WorkflowMode.Standard,
            Status: WorkflowLifecycleStatus.Draft,
            LatestVersionNumber: 1,
            CreatorId: 1L,
            CreatedAt: new DateTime(2025, 1, 1),
            UpdatedAt: new DateTime(2025, 6, 1),
            PublishedAt: null);

        var json = JsonSerializer.Serialize(item, JsonOpts);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("id", out var id));
        Assert.Equal(100, id.GetInt64());
        Assert.True(doc.RootElement.TryGetProperty("name", out var name));
        Assert.Equal("My Workflow", name.GetString());
        Assert.True(doc.RootElement.TryGetProperty("publishedAt", out var publishedAt));
        Assert.Equal(JsonValueKind.Null, publishedAt.ValueKind);
    }

    [Fact]
    public void WorkflowV2RunResult_WithAllFields_ShouldRoundtrip()
    {
        var result = new WorkflowV2RunResult(
            ExecutionId: "exec-123",
            Status: ExecutionStatus.Completed,
            OutputsJson: """{"output":"hello"}""",
            ErrorMessage: null,
            DebugNodeKey: null,
            StepResult: null);

        var json = JsonSerializer.Serialize(result, JsonOpts);
        var restored = JsonSerializer.Deserialize<WorkflowV2RunResult>(json, JsonOpts);

        Assert.NotNull(restored);
        Assert.Equal("exec-123", restored.ExecutionId);
        Assert.Equal(ExecutionStatus.Completed, restored.Status);
        Assert.Equal("""{"output":"hello"}""", restored.OutputsJson);
    }

    [Fact]
    public void WorkflowV2StepResultDto_WithDictionaries_ShouldPreserveData()
    {
        var inputs = new Dictionary<string, JsonElement>
        {
            ["input"] = JsonDocument.Parse("\"hello\"").RootElement
        };
        var outputs = new Dictionary<string, JsonElement>
        {
            ["output"] = JsonDocument.Parse("42").RootElement
        };

        var step = new WorkflowV2StepResultDto(
            ExecutionId: "exec-1",
            NodeKey: "text_1",
            NodeType: WorkflowNodeType.TextProcessor,
            Status: ExecutionStatus.Completed,
            StartedAt: DateTime.UtcNow,
            CompletedAt: DateTime.UtcNow.AddSeconds(1),
            DurationMs: 1000,
            Inputs: inputs,
            Outputs: outputs);

        var json = JsonSerializer.Serialize(step, JsonOpts);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("nodeKey", out var nodeKey));
        Assert.Equal("text_1", nodeKey.GetString());
        Assert.True(doc.RootElement.TryGetProperty("inputs", out _));
        Assert.True(doc.RootElement.TryGetProperty("outputs", out _));
    }

    [Fact]
    public void WorkflowV2RunTraceDto_ShouldSerializeSteps()
    {
        var trace = new WorkflowV2RunTraceDto(
            ExecutionId: "exec-999",
            WorkflowId: 1L,
            Status: ExecutionStatus.Completed,
            StartedAt: DateTime.UtcNow,
            CompletedAt: DateTime.UtcNow.AddSeconds(5),
            DurationMs: 5000,
            Steps: new[]
            {
                new WorkflowV2StepResultDto(
                    "exec-999", "entry_1", WorkflowNodeType.Entry, ExecutionStatus.Completed)
            });

        var json = JsonSerializer.Serialize(trace, JsonOpts);
        var restored = JsonSerializer.Deserialize<WorkflowV2RunTraceDto>(json, JsonOpts);

        Assert.NotNull(restored);
        Assert.Equal("exec-999", restored.ExecutionId);
        Assert.Equal(ExecutionStatus.Completed, restored.Status);
        Assert.NotNull(restored.Steps);
        Assert.Single(restored.Steps);
        Assert.Equal("entry_1", restored.Steps[0].NodeKey);
    }

    [Fact]
    public void WorkflowVersionDiff_HasChanges_WhenNodesAdded()
    {
        var diff = new WorkflowVersionDiff(
            WorkflowId: 1L,
            FromVersionId: 10L,
            FromVersionNumber: 1,
            ToVersionId: 11L,
            ToVersionNumber: 2,
            AddedNodeKeys: new[] { "new_node" },
            RemovedNodeKeys: Array.Empty<string>(),
            ModifiedNodeKeys: Array.Empty<string>(),
            AddedConnectionCount: 1,
            RemovedConnectionCount: 0,
            HasChanges: true);

        Assert.True(diff.HasChanges);
        Assert.Single(diff.AddedNodeKeys);
        Assert.Equal("new_node", diff.AddedNodeKeys[0]);
    }

    // ─── CanvasValidationResult ───────────────────────────────────────────────

    [Fact]
    public void SseEvent_ShouldHoldEventAndData()
    {
        var ev = new SseEvent("step_result", """{"nodeKey":"text_1"}""");
        Assert.Equal("step_result", ev.Event);
        Assert.Contains("text_1", ev.Data);
    }
}
