using Atlas.Application.AiPlatform.Models;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface ICozeWorkflowPlanCompiler
{
    CozeWorkflowCompileResult Compile(string? schemaJson);
}
