namespace Atlas.Core.Expressions;

/// <summary>
/// 规则链执行器 —— 按顺序评估 if/else/switch/决策表条件，返回首个命中的动作结果。
/// </summary>
public interface IRuleChainExecutor
{
    RuleChainResult Execute(RuleChainModel chain, IReadOnlyDictionary<string, object?> input);
}

public sealed class RuleChainModel
{
    public IReadOnlyList<RuleStep> Steps { get; init; } = [];
    public string? DefaultOutputExpression { get; init; }
}

public sealed class RuleStep
{
    public required string ConditionExpression { get; init; }
    public required string OutputExpression { get; init; }
    public int Priority { get; init; }
}

public sealed class RuleChainResult
{
    public bool IsMatched { get; init; }
    public object? Output { get; init; }
    public int? MatchedStepIndex { get; init; }

    public static RuleChainResult NoMatch() => new() { IsMatched = false };
    public static RuleChainResult Matched(object? output, int stepIndex)
        => new() { IsMatched = true, Output = output, MatchedStepIndex = stepIndex };
}
