using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IQueryRewriter
{
    Task<IReadOnlyList<string>> RewriteAsync(
        TenantId tenantId,
        string query,
        int maxQueries = 3,
        CancellationToken cancellationToken = default);
}

public interface IRetriever
{
    Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK = 8,
        CancellationToken cancellationToken = default);
}

public interface IReranker
{
    Task<IReadOnlyList<RagSearchResult>> RerankAsync(
        string query,
        IReadOnlyList<RagSearchResult> candidates,
        int topK = 8,
        CancellationToken cancellationToken = default);
}

public interface IEvidenceScorer
{
    Task<RagEvidenceScore> ScoreAsync(
        string query,
        RagSearchResult evidence,
        CancellationToken cancellationToken = default);
}

public interface IAnswerSynthesizer
{
    Task<RagAnswerSynthesis> SynthesizeAsync(
        string query,
        IReadOnlyList<RagSearchResult> evidence,
        CancellationToken cancellationToken = default);
}

public interface IVerificationEngine
{
    Task<RagVerificationResult> VerifyAsync(
        string query,
        RagAnswerSynthesis answer,
        IReadOnlyList<RagSearchResult> evidence,
        CancellationToken cancellationToken = default);
}

public interface IRetrievalPipeline
{
    Task<RagPipelineResult> ExecuteAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        RagPipelineOptions? options = null,
        CancellationToken cancellationToken = default);
}

public interface IKnowledgeGraphProvider
{
    Task<IReadOnlyList<RagSearchResult>> SearchAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default);
}
