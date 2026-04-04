using Atlas.Application.AiPlatform.Models;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface ICodeExecutionService
{
    Task<CodeExecutionResult> ExecuteAsync(CodeExecutionRequest request, CancellationToken cancellationToken);
}
