using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IPromptGuard
{
    Task<PromptGuardResult> CheckAsync(
        TenantId tenantId,
        string input,
        CancellationToken cancellationToken = default);
}

public interface IPiiDetector
{
    PiiDetectionResult Detect(string text);
}
