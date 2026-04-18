using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services.WorkflowEngine;
using Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Atlas.SecurityPlatform.Tests.Services.AiPlatform;

/// <summary>
/// D6：DatabaseNl2SqlNodeExecutor 计划解析的健壮性。
/// 覆盖：```json fence、解释性前缀、非法 op（应被过滤而不是炸）。
/// </summary>
public sealed class DatabaseNl2SqlNodeExecutorTests
{
    [Fact]
    public async Task Execute_LlmReturnsJsonInsideMarkdownFence_ShouldParsePlanSuccessfully()
    {
        using var harness = await BuildHarnessAsync();
        var llm = StubLlm(@"```json
{ ""fields"": [""id""], ""clauses"": [{ ""field"": ""id"", ""op"": ""eq"", ""value"": ""r1"", ""logic"": ""and"" }], ""limit"": 50 }
```");
        var executor = new DatabaseNl2SqlNodeExecutor(harness.Inner.Db, llm, NullLogger<DatabaseNl2SqlNodeExecutor>.Instance);
        var result = await executor.ExecuteAsync(BuildContext(harness, "查询 id=r1"), CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.Outputs.ContainsKey("nl2sql_plan"));
        Assert.True(result.Outputs.ContainsKey("nl2sql_question"));
        Assert.Equal(1, result.Outputs["record_count"].GetInt32());
    }

    [Fact]
    public async Task Execute_LlmReturnsExplanatoryPrefixThenJson_ShouldStillExtractObject()
    {
        using var harness = await BuildHarnessAsync();
        var llm = StubLlm(
            "好的，根据用户问题我生成如下查询计划：\n" +
            "{ \"fields\": [\"id\"], \"clauses\": [{\"field\":\"id\",\"op\":\"eq\",\"value\":\"r2\",\"logic\":\"and\"}], \"limit\": 10 }\n" +
            "希望对您有帮助。");
        var executor = new DatabaseNl2SqlNodeExecutor(harness.Inner.Db, llm, NullLogger<DatabaseNl2SqlNodeExecutor>.Instance);
        var result = await executor.ExecuteAsync(BuildContext(harness, "找 r2"), CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(1, result.Outputs["record_count"].GetInt32());
    }

    [Fact]
    public async Task Execute_LlmReturnsInvalidOp_ShouldDropTheClause_ButStillSucceedAsNoFilter()
    {
        using var harness = await BuildHarnessAsync();
        // op="lol" 不在 Compare 白名单里 → 全部 false → 过滤掉所有行；节点本身仍 success。
        var llm = StubLlm(
            "{ \"fields\": [\"id\"], \"clauses\": [{\"field\":\"id\",\"op\":\"lol\",\"value\":\"r1\",\"logic\":\"and\"}], \"limit\": 10 }");
        var executor = new DatabaseNl2SqlNodeExecutor(harness.Inner.Db, llm, NullLogger<DatabaseNl2SqlNodeExecutor>.Instance);
        var result = await executor.ExecuteAsync(BuildContext(harness, "随便问"), CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(0, result.Outputs["record_count"].GetInt32());
    }

    [Fact]
    public async Task Execute_LlmThrows_ShouldFailGracefullyWithSanitizedMessage()
    {
        using var harness = await BuildHarnessAsync();
        var llm = Substitute.For<ILlmProviderFactory>();
        var provider = Substitute.For<ILlmProvider>();
        provider.ChatAsync(Arg.Any<ChatCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<ChatCompletionResult>>(_ => throw new InvalidOperationException("internal-llm-failure-detail"));
        llm.GetLlmProvider(Arg.Any<string?>()).Returns(provider);

        var executor = new DatabaseNl2SqlNodeExecutor(harness.Inner.Db, llm, NullLogger<DatabaseNl2SqlNodeExecutor>.Instance);
        var result = await executor.ExecuteAsync(BuildContext(harness, "任意"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        // M1：LLM 内部异常细节不应外泄给前端。
        Assert.DoesNotContain("internal-llm-failure-detail", result.ErrorMessage);
    }

    private static async Task<NL2SqlHarness> BuildHarnessAsync()
    {
        var inner = new AiDatabaseTestHarness();
        var db = await inner.SeedDatabaseAsync(tableSchema:
            "[{\"name\":\"id\",\"type\":\"string\"}]");
        await inner.Service.CreateRecordsBulkAsync(
            AiDatabaseTestHarness.Tenant, db.Id,
            new AiDatabaseRecordBulkCreateRequest(
            [
                "{\"id\":\"r1\"}",
                "{\"id\":\"r2\"}"
            ]),
            CancellationToken.None);
        return new NL2SqlHarness(inner, db.Id);
    }

    private static NodeExecutionContext BuildContext(NL2SqlHarness h, string question)
    {
        var config = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["databaseInfoId"] = JsonSerializer.SerializeToElement(h.DatabaseId),
            ["prompt"] = JsonSerializer.SerializeToElement(question),
            ["model"] = JsonSerializer.SerializeToElement("test-model")
        };
        return h.Inner.BuildNodeContext("nl2sql-1", WorkflowNodeType.DatabaseNl2Sql, config);
    }

    private static ILlmProviderFactory StubLlm(string content)
    {
        var factory = Substitute.For<ILlmProviderFactory>();
        var provider = Substitute.For<ILlmProvider>();
        provider.ChatAsync(Arg.Any<ChatCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatCompletionResult(content)));
        factory.GetLlmProvider(Arg.Any<string?>()).Returns(provider);
        return factory;
    }

    private sealed class NL2SqlHarness : IDisposable
    {
        public NL2SqlHarness(AiDatabaseTestHarness inner, long databaseId)
        {
            Inner = inner;
            DatabaseId = databaseId;
        }

        public AiDatabaseTestHarness Inner { get; }
        public long DatabaseId { get; }

        public void Dispose() => Inner.Dispose();
    }
}
