namespace Atlas.Core.Expressions;

/// <summary>
/// 决策表执行器 —— 对输入数据依次匹配行规则，返回首个命中行的输出。
/// </summary>
public interface IDecisionTableExecutor
{
    DecisionTableResult Execute(DecisionTableModel table, IReadOnlyDictionary<string, object?> input);
}

/// <summary>
/// 决策表模型（行×列规则矩阵）。
/// </summary>
public sealed class DecisionTableModel
{
    public IReadOnlyList<DecisionInputColumn> InputColumns { get; init; } = [];
    public IReadOnlyList<DecisionOutputColumn> OutputColumns { get; init; } = [];
    public IReadOnlyList<DecisionRow> Rows { get; init; } = [];
    public DecisionHitPolicy HitPolicy { get; init; } = DecisionHitPolicy.First;
}

public sealed class DecisionInputColumn
{
    public required string Name { get; init; }
    public ExprType Type { get; init; } = ExprType.Any;
    public string? Expression { get; init; }
}

public sealed class DecisionOutputColumn
{
    public required string Name { get; init; }
    public ExprType Type { get; init; } = ExprType.Any;
}

public sealed class DecisionRow
{
    public int Priority { get; init; }
    public IReadOnlyList<string> InputEntries { get; init; } = [];
    public IReadOnlyList<string> OutputEntries { get; init; } = [];
}

public sealed class DecisionTableResult
{
    public bool IsMatched { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> MatchedOutputs { get; init; } = [];

    public static DecisionTableResult NoMatch() => new() { IsMatched = false };
    public static DecisionTableResult Matched(IReadOnlyList<IReadOnlyDictionary<string, object?>> outputs)
        => new() { IsMatched = true, MatchedOutputs = outputs };
}

public enum DecisionHitPolicy
{
    First = 1,
    Collect = 2,
    RuleOrder = 3,
}
