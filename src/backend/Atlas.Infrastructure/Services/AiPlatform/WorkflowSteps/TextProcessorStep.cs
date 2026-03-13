using System.Text.RegularExpressions;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.Infrastructure.Services.AiPlatform.WorkflowSteps;

public sealed class TextProcessorStep : StepBody
{
    public string Operation { get; set; } = "template";
    public string? Input { get; set; }
    public string? Pattern { get; set; }
    public string? Replacement { get; set; }
    public string? AdditionalText { get; set; }
    public string OutputKey { get; set; } = "textOutput";

    public override ExecutionResult Run(Atlas.WorkflowCore.Abstractions.IStepExecutionContext context)
    {
        var data = WorkflowStepDataHelper.EnsureDataDictionary(context);
        var source = WorkflowStepDataHelper.ResolveTemplate(Input, data);
        var result = Operation.ToLowerInvariant() switch
        {
            "replace" => source.Replace(
                WorkflowStepDataHelper.ResolveTemplate(Pattern, data),
                WorkflowStepDataHelper.ResolveTemplate(Replacement, data),
                StringComparison.Ordinal),
            "concat" => source + WorkflowStepDataHelper.ResolveTemplate(AdditionalText, data),
            "extract" => Extract(source, WorkflowStepDataHelper.ResolveTemplate(Pattern, data)),
            _ => source
        };

        data[OutputKey] = result;
        return ExecutionResult.Next();
    }

    private static string Extract(string input, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return string.Empty;
        }

        var match = Regex.Match(input, pattern, RegexOptions.Singleline);
        if (!match.Success)
        {
            return string.Empty;
        }

        return match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
    }
}
