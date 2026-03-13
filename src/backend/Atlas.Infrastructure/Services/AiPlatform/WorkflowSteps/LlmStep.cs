using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.Infrastructure.Services.AiPlatform.WorkflowSteps;

public sealed class LlmStep : StepBodyAsync
{
    private readonly ILlmProviderFactory _llmProviderFactory;

    public LlmStep(ILlmProviderFactory llmProviderFactory)
    {
        _llmProviderFactory = llmProviderFactory;
    }

    public string? Prompt { get; set; }
    public string? Model { get; set; }
    public string? Provider { get; set; }
    public float? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public string OutputKey { get; set; } = "llmOutput";

    public override async Task<ExecutionResult> RunAsync(Atlas.WorkflowCore.Abstractions.IStepExecutionContext context)
    {
        var data = WorkflowStepDataHelper.EnsureDataDictionary(context);
        var prompt = WorkflowStepDataHelper.ResolveTemplate(Prompt, data);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            data[OutputKey] = string.Empty;
            return ExecutionResult.Next();
        }

        var provider = _llmProviderFactory.GetLlmProvider(Provider);
        var request = new ChatCompletionRequest(
            string.IsNullOrWhiteSpace(Model) ? "gpt-4o-mini" : Model,
            [new ChatMessage("user", prompt)],
            Temperature,
            MaxTokens,
            Provider);
        var result = await provider.ChatAsync(request, context.CancellationToken);
        data[OutputKey] = result.Content;
        return ExecutionResult.Next();
    }
}
