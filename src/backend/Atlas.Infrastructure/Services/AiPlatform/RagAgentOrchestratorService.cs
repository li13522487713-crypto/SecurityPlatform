#pragma warning disable SKEXP0110
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class RagAgentOrchestratorService : IAgentOrchestrator
{
    private readonly IKernelFactory _kernelFactory;
    private readonly IRetrievalPipeline _retrievalPipeline;
    private readonly IQueryRewriter _queryRewriter;
    private readonly IRetriever _retriever;
    private readonly IEvidenceScorer _evidenceScorer;
    private readonly IVerificationEngine _verificationEngine;
    private readonly AgenticRagRunHistoryRepository _agenticRagRunHistoryRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly ILogger<RagAgentOrchestratorService> _logger;

    public RagAgentOrchestratorService(
        IKernelFactory kernelFactory,
        IRetrievalPipeline retrievalPipeline,
        IQueryRewriter queryRewriter,
        IRetriever retriever,
        IEvidenceScorer evidenceScorer,
        IVerificationEngine verificationEngine,
        AgenticRagRunHistoryRepository agenticRagRunHistoryRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        ILogger<RagAgentOrchestratorService> logger)
    {
        _kernelFactory = kernelFactory;
        _retrievalPipeline = retrievalPipeline;
        _queryRewriter = queryRewriter;
        _retriever = retriever;
        _evidenceScorer = evidenceScorer;
        _verificationEngine = verificationEngine;
        _agenticRagRunHistoryRepository = agenticRagRunHistoryRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _logger = logger;
    }

    public async Task<AgenticRagQueryResponse> OrchestrateAsync(
        TenantId tenantId,
        AgenticRagQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = (request.Query ?? string.Empty).Trim();
        if (query.Length == 0)
        {
            var emptyResponse = new AgenticRagQueryResponse(
                "agent-group-chat",
                "问题为空。",
                0f,
                [],
                [Trace("router", "query-empty")]);
            await PersistHistoryAsync(tenantId, query, emptyResponse, cancellationToken);
            return emptyResponse;
        }

        var topK = Math.Clamp(request.TopK <= 0 ? 6 : request.TopK, 1, 12);
        var pipelineResult = await _retrievalPipeline.ExecuteAsync(
            tenantId,
            request.KnowledgeBaseIds ?? [],
            query,
            new RagPipelineOptions(
                EnableQueryRewrite: true,
                EnableRerank: true,
                EnableEvidenceScoring: true,
                EnableVerification: true,
                EnableAutoRetry: true,
                TopK: topK,
                CandidateTopK: Math.Max(topK * 2, 12),
                MaxRetries: 1,
                EvidenceThreshold: 0.08f),
            cancellationToken);
        var traces = pipelineResult.Traces
            .Select(item => Trace($"pipeline.{item.Stage}", item.Detail))
            .ToList();

        if (pipelineResult.Evidence.Count == 0)
        {
            traces.Add(Trace("router", "pipeline-no-evidence"));
            var noEvidenceResponse = new AgenticRagQueryResponse(
                "agent-group-chat",
                pipelineResult.Answer,
                pipelineResult.Confidence,
                [],
                traces);
            await PersistHistoryAsync(tenantId, query, noEvidenceResponse, cancellationToken);
            return noEvidenceResponse;
        }

        try
        {
            var kernel = await _kernelFactory.CreateAsync(tenantId, null, null, cancellationToken);
            kernel.Plugins.Add(BuildToolPlugin(tenantId, request, pipelineResult.Evidence));

            var agents = BuildAgents(kernel);
            var orchestration = new GroupChatOrchestration(
                new RoundRobinGroupChatManager
                {
                    MaximumInvocationCount = 14
                },
                agents)
            {
                ResponseCallback = async message =>
                {
                    var author = message.AuthorName ?? "unknown";
                    var content = NormalizeText(message);
                    traces.Add(Trace($"agent.{author}", content));
                    await ValueTask.CompletedTask;
                }
            };

            var runtime = new InProcessRuntime();
            await runtime.StartAsync();
            var prompt = BuildInputPrompt(query, pipelineResult);
            var orchestrationResult = await orchestration.InvokeAsync(prompt, runtime);
            var finalAnswer = NormalizeText(await orchestrationResult.GetValueAsync());
            if (string.IsNullOrWhiteSpace(finalAnswer))
            {
                finalAnswer = pipelineResult.Answer;
            }

            var finalSynthesis = new RagAnswerSynthesis(finalAnswer, pipelineResult.Citations, pipelineResult.Confidence);
            var verifyResult = await _verificationEngine.VerifyAsync(
                query,
                finalSynthesis,
                pipelineResult.Evidence,
                cancellationToken);
            traces.Add(Trace("critic", $"passed={verifyResult.IsPassed}, safety={verifyResult.SafetyScore:F2}"));

            if (!verifyResult.IsPassed)
            {
                finalAnswer = $"{pipelineResult.Answer}\n\n[系统提示] 编排结果未通过校验，已回退到已验证答案。";
            }

            var scoreMap = pipelineResult.Evidence.ToDictionary(item => item.ChunkId, item => item.Score);
            var citations = pipelineResult.Citations
                .Select(item => new AgenticRagCitation(
                    item.KnowledgeBaseId,
                    item.DocumentId,
                    item.ChunkId,
                    item.DocumentName,
                    scoreMap.GetValueOrDefault(item.ChunkId)))
                .ToArray();
            var confidence = !verifyResult.IsPassed
                ? Math.Clamp(pipelineResult.Confidence * 0.8f, 0f, 1f)
                : Math.Clamp((pipelineResult.Confidence * 0.6f) + (verifyResult.SafetyScore * 0.4f), 0f, 1f);
            var finalResponse = new AgenticRagQueryResponse(
                "agent-group-chat",
                finalAnswer,
                confidence,
                citations,
                traces);
            await PersistHistoryAsync(tenantId, query, finalResponse, cancellationToken);
            return finalResponse;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Agent group chat orchestration failed, fallback to pipeline answer.");
            traces.Add(Trace("orchestrator.fallback", ex.Message));
            var fallbackCitations = pipelineResult.Citations
                .Select(item => new AgenticRagCitation(item.KnowledgeBaseId, item.DocumentId, item.ChunkId, item.DocumentName, 0f))
                .ToArray();
            var fallbackResponse = new AgenticRagQueryResponse(
                "agent-group-chat",
                pipelineResult.Answer,
                pipelineResult.Confidence,
                fallbackCitations,
                traces);
            await PersistHistoryAsync(tenantId, query, fallbackResponse, cancellationToken);
            return fallbackResponse;
        }
    }

    private KernelPlugin BuildToolPlugin(
        TenantId tenantId,
        AgenticRagQueryRequest request,
        IReadOnlyList<RagSearchResult> seedEvidence)
    {
        var rewriteFunction = KernelFunctionFactory.CreateFromMethod(
            method: async (string inputQuery, int maxQueries, CancellationToken ct) =>
            {
                var rewrites = await _queryRewriter.RewriteAsync(tenantId, inputQuery, maxQueries <= 0 ? 3 : maxQueries, ct);
                return JsonSerializer.Serialize(rewrites);
            },
            options: new KernelFunctionFromMethodOptions
            {
                FunctionName = "rewrite_query",
                Description = "将原始问题改写为多个检索查询。"
            });

        var planFunction = KernelFunctionFactory.CreateFromMethod(
            method: (string inputQuery, int topK) => JsonSerializer.Serialize(new
            {
                strategy = "hybrid+graph",
                query = inputQuery,
                topK = topK <= 0 ? request.TopK : topK,
                route = "agentic"
            }),
            options: new KernelFunctionFromMethodOptions
            {
                FunctionName = "generate_retrieval_plan",
                Description = "根据问题生成检索计划。"
            });

        var retrieveFunction = KernelFunctionFactory.CreateFromMethod(
            method: async (string inputQuery, int topK, CancellationToken ct) =>
            {
                var rows = await _retriever.RetrieveAsync(
                    tenantId,
                    request.KnowledgeBaseIds,
                    inputQuery,
                    topK <= 0 ? request.TopK : topK,
                    ct);
                return JsonSerializer.Serialize(rows.Take(6).Select(item => new
                {
                    item.KnowledgeBaseId,
                    item.DocumentId,
                    item.ChunkId,
                    item.Score,
                    item.DocumentName
                }));
            },
            options: new KernelFunctionFromMethodOptions
            {
                FunctionName = "retrieve_evidence",
                Description = "执行检索并返回证据摘要。"
            });

        var judgeFunction = KernelFunctionFactory.CreateFromMethod(
            method: async (string inputQuery, string evidenceText, CancellationToken ct) =>
            {
                var pseudoEvidence = new RagSearchResult(
                    KnowledgeBaseId: seedEvidence[0].KnowledgeBaseId,
                    DocumentId: seedEvidence[0].DocumentId,
                    ChunkId: seedEvidence[0].ChunkId,
                    Content: evidenceText,
                    Score: 0.5f,
                    DocumentName: seedEvidence[0].DocumentName,
                    DocumentCreatedAt: seedEvidence[0].DocumentCreatedAt);
                var score = await _evidenceScorer.ScoreAsync(inputQuery, pseudoEvidence, ct);
                return JsonSerializer.Serialize(score);
            },
            options: new KernelFunctionFromMethodOptions
            {
                FunctionName = "judge_evidence",
                Description = "对证据相关性与可信度进行评分。"
            });

        var verifyFunction = KernelFunctionFactory.CreateFromMethod(
            method: async (string inputQuery, string answer, CancellationToken ct) =>
            {
                var synthesis = new RagAnswerSynthesis(answer, [], 0.5f);
                var verification = await _verificationEngine.VerifyAsync(
                    inputQuery,
                    synthesis,
                    seedEvidence,
                    ct);
                return JsonSerializer.Serialize(verification);
            },
            options: new KernelFunctionFromMethodOptions
            {
                FunctionName = "verify_answer",
                Description = "校验答案是否忠于证据且符合安全要求。"
            });

        return KernelPluginFactory.CreateFromFunctions(
            "rag_agent_tools",
            "Agentic RAG 工具箱",
            [rewriteFunction, planFunction, retrieveFunction, judgeFunction, verifyFunction]);
    }

    private static ChatCompletionAgent[] BuildAgents(Kernel kernel)
    {
        return
        [
            CreateAgent(kernel, "RouterAgent", "你负责识别问题类型并选择后续处理路径。"),
            CreateAgent(kernel, "QueryUnderstandingAgent", "你负责意图识别、实体抽取、查询改写。可调用 rewrite_query。"),
            CreateAgent(kernel, "RetrievalPlannerAgent", "你负责制定检索计划。可调用 generate_retrieval_plan。"),
            CreateAgent(kernel, "RetrieverAgent", "你负责执行检索并提取候选证据。可调用 retrieve_evidence。"),
            CreateAgent(kernel, "EvidenceJudgeAgent", "你负责筛选高质量证据。可调用 judge_evidence。"),
            CreateAgent(kernel, "SynthesisAgent", "你负责基于证据生成答案，必须引用来源编号。"),
            CreateAgent(kernel, "CriticVerifierAgent", "你负责最终事实校验与风险审查。可调用 verify_answer。")
        ];
    }

    private static ChatCompletionAgent CreateAgent(Kernel kernel, string name, string instructions)
    {
        return new ChatCompletionAgent
        {
            Name = name,
            Description = instructions,
            Instructions = instructions,
            Kernel = kernel,
            Arguments = new KernelArguments(new OpenAIPromptExecutionSettings
            {
                Temperature = 0.2f,
                MaxTokens = 800,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
                    functions: null,
                    autoInvoke: true,
                    options: new FunctionChoiceBehaviorOptions
                    {
                        AllowParallelCalls = true,
                        AllowConcurrentInvocation = true
                    })
            })
        };
    }

    private static string BuildInputPrompt(string query, RagPipelineResult pipelineResult)
    {
        var evidenceDigest = string.Join(
            "\n\n",
            pipelineResult.Evidence
                .Take(4)
                .Select((item, index) =>
                    $"[C{index + 1}] kb={item.KnowledgeBaseId}, doc={item.DocumentId}, chunk={item.ChunkId}, score={item.Score:F3}\n{item.Content}"));
        return
            $"用户问题：{query}\n\n初步答案：{pipelineResult.Answer}\n\n证据：\n{evidenceDigest}\n\n请七个角色协作，输出最终回答。";
    }

    private static AgenticRagStepTrace Trace(string stage, string detail)
        => new(stage, detail, DateTimeOffset.UtcNow.ToString("O"));

    private static string NormalizeText(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is string text)
        {
            return text.Trim();
        }

        if (value is ChatMessageContent messageContent)
        {
            return messageContent.Content?.Trim() ?? string.Empty;
        }

        return value.ToString()?.Trim() ?? string.Empty;
    }

    private async Task PersistHistoryAsync(
        TenantId tenantId,
        string query,
        AgenticRagQueryResponse response,
        CancellationToken cancellationToken)
    {
        var sessionKey = BuildSessionKey(query);
        var entity = new AgenticRagRunHistory(
            tenantId,
            _idGeneratorAccessor.NextId(),
            sessionKey,
            query,
            response.Answer,
            JsonSerializer.Serialize(response.Citations),
            JsonSerializer.Serialize(response.Traces));
        await _agenticRagRunHistoryRepository.AddAsync(entity, cancellationToken);
    }

    private static string BuildSessionKey(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "empty";
        }

        var normalized = query.Trim().ToLowerInvariant();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized)));
        return hash[..Math.Min(hash.Length, 64)];
    }
}
