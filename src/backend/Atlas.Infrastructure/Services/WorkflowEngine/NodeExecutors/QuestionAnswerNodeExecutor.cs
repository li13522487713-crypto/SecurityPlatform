using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 问答节点：首次执行触发中断等待用户输入；恢复后读取答案并输出。
/// </summary>
public sealed class QuestionAnswerNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.QuestionAnswer;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        var answerPath = context.GetConfigString("answerPath", "question_answer");
        if (context.TryResolveVariable(answerPath, out var answer))
        {
            outputs["question_answer"] = answer.Clone();
            outputs["answer_received"] = JsonSerializer.SerializeToElement(true);
            outputs["answer_path"] = VariableResolver.CreateStringElement(answerPath);
            return Task.FromResult(new NodeExecutionResult(true, outputs));
        }

        var question = context.ReplaceVariables(context.GetConfigString("question", "请提供答案。"));
        var answerType = context.GetConfigString("answerType", "free");
        var maxAnswerCount = Math.Clamp(context.GetConfigInt32("maxAnswerCount", 1), 1, 100);
        outputs["question"] = VariableResolver.CreateStringElement(question);
        outputs["answer_type"] = VariableResolver.CreateStringElement(answerType);
        outputs["max_answer_count"] = JsonSerializer.SerializeToElement(maxAnswerCount);
        outputs["answer_received"] = JsonSerializer.SerializeToElement(false);
        outputs["answer_path"] = VariableResolver.CreateStringElement(answerPath);

        if (VariableResolver.TryGetConfigValue(context.Node.Config, "fixedChoices", out var fixedChoices))
        {
            outputs["fixed_choices"] = fixedChoices.Clone();
        }

        return Task.FromResult(new NodeExecutionResult(
            Success: false,
            Outputs: outputs,
            ErrorMessage: "QuestionAnswer 等待用户输入。",
            InterruptType: InterruptType.QuestionAnswer));
    }
}
