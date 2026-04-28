using System.Text.Json;
using System.Threading.Channels;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Abstractions.Knowledge;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.AiPlatform.ValueObjects;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.WorkflowEngine;
using Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SqlSugar;

namespace Atlas.SecurityPlatform.Tests.Services.AiPlatform;

/// <summary>
/// v5 §38 / 计划 G9：KnowledgeRetrieverNodeExecutor & KnowledgeIndexerNodeExecutor 单测。
/// 验证 filters / callerContextOverride / chunkingProfile / mode (overwrite) 全部正确传递；
/// 验证输出 key 全部 camelCase。
/// </summary>
public sealed class KnowledgeNodeExecutorTests
{
    private static readonly TenantId TestTenantId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    private static NodeExecutionContext BuildContext(
        IServiceProvider services,
        WorkflowNodeType type,
        string nodeKey,
        Dictionary<string, JsonElement> config)
    {
        var node = new NodeSchema(
            nodeKey,
            type,
            nodeKey,
            config,
            new NodeLayout(0, 0, 200, 100));
        return new NodeExecutionContext(
            node,
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase),
            services,
            TestTenantId,
            workflowId: 1,
            executionId: 1,
            workflowCallStack: Array.Empty<long>(),
            eventChannel: null);
    }

    [Fact]
    public async Task KnowledgeRetriever_Should_Pass_Filters_And_CallerContextOverride_AndEmit_CamelCase()
    {
        // Arrange — mock IRagRetrievalService capturing the request
        RetrievalRequest? capturedRequest = null;
        var ragService = Substitute.For<IRagRetrievalService>();
        ragService.SearchWithProfileAsync(
                Arg.Any<TenantId>(),
                Arg.Do<RetrievalRequest>(r => capturedRequest = r),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<RetrievalRequest>();
                var log = new RetrievalLogDto(
                    TraceId: "trc_xxx",
                    KnowledgeBaseId: request.KnowledgeBaseIds[0],
                    RawQuery: request.Query,
                    CallerContext: request.CallerContext,
                    Candidates: Array.Empty<RetrievalCandidate>(),
                    Reranked: Array.Empty<RetrievalCandidate>(),
                    FinalContext: "final-context-mock",
                    EmbeddingModel: "test-embed",
                    VectorStore: "test-vector",
                    LatencyMs: 12,
                    CreatedAt: DateTime.UtcNow,
                    RewrittenQuery: "rewritten",
                    Filters: request.Filters);
                return Task.FromResult(RetrievalResponseDto.FromLog(log));
            });

        var services = new ServiceCollection().BuildServiceProvider();
        var executor = new KnowledgeRetrieverNodeExecutor(ragService);

        var config = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["knowledgeIds"] = JsonSerializer.SerializeToElement(new long[] { 11L }),
            ["query"] = JsonSerializer.SerializeToElement("hello world"),
            ["topK"] = JsonSerializer.SerializeToElement(7),
            ["filters"] = JsonSerializer.SerializeToElement(new Dictionary<string, string>
            {
                ["tag"] = "security",
                ["namespace"] = "prod"
            }),
            ["callerContextOverride"] = JsonSerializer.SerializeToElement(new
            {
                callerType = 2,
                userId = "u-override",
                preset = 1
            }),
            ["debug"] = JsonSerializer.SerializeToElement(true)
        };

        var ctx = BuildContext(services, WorkflowNodeType.KnowledgeRetriever, "kr1", config);

        // Act
        var result = await executor.ExecuteAsync(ctx, CancellationToken.None);

        // Assert outputs
        Assert.True(result.Success);
        Assert.True(result.Outputs.ContainsKey("traceId"), "traceId should be camelCase");
        Assert.True(result.Outputs.ContainsKey("finalContext"), "finalContext should be camelCase");
        Assert.True(result.Outputs.ContainsKey("candidates"), "candidates should be present");
        Assert.True(result.Outputs.ContainsKey("rewrittenQuery"), "rewrittenQuery should be camelCase");
        Assert.True(result.Outputs.ContainsKey("latencyMs"), "latencyMs should be present");
        Assert.Equal("trc_xxx", result.Outputs["traceId"].GetString());
        Assert.Equal("final-context-mock", result.Outputs["finalContext"].GetString());

        // Assert capture
        Assert.NotNull(capturedRequest);
        Assert.Equal("hello world", capturedRequest!.Query);
        Assert.Equal(7, capturedRequest.TopK);
        Assert.NotNull(capturedRequest.Filters);
        Assert.Equal("security", capturedRequest.Filters!["tag"]);
        Assert.Equal("prod", capturedRequest.Filters["namespace"]);
        Assert.True(capturedRequest.Debug);

        // CallerContext: override should be merged with default (Workflow caller)
        Assert.Equal("u-override", capturedRequest.CallerContext.UserId);
        Assert.Equal(RetrievalCallerPreset.WorkflowDebug, capturedRequest.CallerContext.Preset);
    }

    private static (SqlSugarScope Db, KnowledgeDocumentRepository Repo, string TempDir) CreateInMemoryDocRepo()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"atlas-knodes-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "kn.db");
        var db = new SqlSugarScope(new ConnectionConfig
        {
            ConnectionString = $"Data Source={dbPath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = (property, column) =>
                {
                    if (property.Name == nameof(Atlas.Core.Abstractions.TenantEntity.TenantId)
                        && property.PropertyType == typeof(TenantId))
                    {
                        column.IsIgnore = true;
                        return;
                    }
                    if (property.Name == "Id" && property.PropertyType == typeof(long))
                    {
                        column.IsPrimarykey = true;
                        column.IsIdentity = false;
                    }
                    if (Nullable.GetUnderlyingType(property.PropertyType) is not null)
                    {
                        column.IsNullable = true;
                    }
                    else if (property.PropertyType == typeof(string))
                    {
                        // KnowledgeDocument.ErrorMessage 等 nullable string 字段 SQLite 默认 NOT NULL；统一放宽
                        column.IsNullable = true;
                    }
                }
            }
        });
        db.CodeFirst.InitTables(typeof(KnowledgeDocument));
        var repo = new KnowledgeDocumentRepository(db);
        return (db, repo, tempDir);
    }

    [Fact]
    public async Task KnowledgeIndexer_Should_Parse_ChunkingProfile_And_OverwriteMode()
    {
        // Arrange — real in-memory SqlSugar + sealed KnowledgeDocumentRepository
        var (db, docRepo, tempDir) = CreateInMemoryDocRepo();
        try
        {

        ChunkingProfile? capturedProfile = null;
        KnowledgeIndexMode? capturedMode = null;
        var indexJobService = Substitute.For<IKnowledgeIndexJobService>();
        indexJobService.EnqueueIndexAsync(
                Arg.Any<TenantId>(),
                Arg.Any<long>(),
                Arg.Any<long>(),
                Arg.Do<ChunkingProfile?>(p => capturedProfile = p),
                Arg.Do<KnowledgeIndexMode>(m => capturedMode = m),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(99L));

        var idGen = Substitute.For<Atlas.Core.Abstractions.IIdGeneratorAccessor>();
        idGen.NextId().Returns(1234L);

        var services = new ServiceCollection().BuildServiceProvider();
        var executor = new KnowledgeIndexerNodeExecutor(docRepo, indexJobService, idGen);

        var config = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["knowledgeId"] = JsonSerializer.SerializeToElement(7L),
            ["fileId"] = JsonSerializer.SerializeToElement(123L),
            ["fileName"] = JsonSerializer.SerializeToElement("doc.pdf"),
            ["chunkingProfile"] = JsonSerializer.SerializeToElement(new
            {
                mode = 1,            // Semantic
                size = 1024,
                overlap = 128
            }),
            ["mode"] = JsonSerializer.SerializeToElement("overwrite")
        };

        var ctx = BuildContext(services, WorkflowNodeType.KnowledgeIndexer, "ki1", config);

        // Act
        var result = await executor.ExecuteAsync(ctx, CancellationToken.None);

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.Outputs.ContainsKey("documentId"));
        Assert.True(result.Outputs.ContainsKey("knowledgeId"));
        Assert.True(result.Outputs.ContainsKey("jobId"));
        Assert.True(result.Outputs.ContainsKey("mode"));
        Assert.Equal("overwrite", result.Outputs["mode"].GetString());
        Assert.Equal(99L, result.Outputs["jobId"].GetInt64());

        // Captured chunkingProfile + mode passed correctly
        Assert.NotNull(capturedProfile);
        Assert.Equal(ChunkingProfileMode.Semantic, capturedProfile!.Mode);
        Assert.Equal(1024, capturedProfile.Size);
        Assert.Equal(128, capturedProfile.Overlap);
        Assert.Equal(KnowledgeIndexMode.Overwrite, capturedMode);
        }
        finally
        {
            db.Dispose();
            try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task KnowledgeIndexer_Should_Default_To_Append_Mode_When_Not_Specified()
    {
        var (db, docRepo, tempDir) = CreateInMemoryDocRepo();
        try
        {

        KnowledgeIndexMode? capturedMode = null;
        var indexJobService = Substitute.For<IKnowledgeIndexJobService>();
        indexJobService.EnqueueIndexAsync(
                Arg.Any<TenantId>(),
                Arg.Any<long>(),
                Arg.Any<long>(),
                Arg.Any<ChunkingProfile?>(),
                Arg.Do<KnowledgeIndexMode>(m => capturedMode = m),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1L));

        var idGen = Substitute.For<Atlas.Core.Abstractions.IIdGeneratorAccessor>();
        idGen.NextId().Returns(2L);
        var services = new ServiceCollection().BuildServiceProvider();
        var executor = new KnowledgeIndexerNodeExecutor(docRepo, indexJobService, idGen);

        var config = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["knowledgeId"] = JsonSerializer.SerializeToElement(7L),
            ["fileId"] = JsonSerializer.SerializeToElement(123L)
        };

        var ctx = BuildContext(services, WorkflowNodeType.KnowledgeIndexer, "ki2", config);
        var result = await executor.ExecuteAsync(ctx, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(KnowledgeIndexMode.Append, capturedMode);
        }
        finally
        {
            db.Dispose();
            try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
        }
    }
}
