using Atlas.Core.Expressions;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Rules;

/// <summary>
/// 决策表执行器 —— 依次匹配行规则，按命中策略返回结果。
/// </summary>
public sealed class DecisionTableExecutor : IDecisionTableExecutor
{
    private readonly ExprEvaluator _evaluator;

    public DecisionTableExecutor(ExprEvaluator evaluator)
    {
        _evaluator = evaluator;
    }

    public DecisionTableResult Execute(DecisionTableModel table, IReadOnlyDictionary<string, object?> input)
    {
        var ctx = ExpressionContext.FromRecord(input);
        var matched = new List<IReadOnlyDictionary<string, object?>>();

        foreach (var row in table.Rows.OrderBy(r => r.Priority))
        {
            if (MatchRow(row, table.InputColumns, ctx))
            {
                var output = BuildOutput(row, table.OutputColumns, ctx);
                matched.Add(output);

                if (table.HitPolicy == DecisionHitPolicy.First) break;
            }
        }

        return matched.Count > 0
            ? DecisionTableResult.Matched(matched)
            : DecisionTableResult.NoMatch();
    }

    private bool MatchRow(DecisionRow row, IReadOnlyList<DecisionInputColumn> columns, ExpressionContext ctx)
    {
        for (int i = 0; i < Math.Min(row.InputEntries.Count, columns.Count); i++)
        {
            var entry = row.InputEntries[i];
            if (string.IsNullOrWhiteSpace(entry) || entry == "-") continue;

            var column = columns[i];
            var expression = BuildConditionExpression(column, entry);
            var ast = _evaluator.ParseAndCache(expression);
            var result = _evaluator.Evaluate(ast, ctx);

            if (result is not true) return false;
        }
        return true;
    }

    private Dictionary<string, object?> BuildOutput(DecisionRow row, IReadOnlyList<DecisionOutputColumn> columns, ExpressionContext ctx)
    {
        var output = new Dictionary<string, object?>();
        for (int i = 0; i < Math.Min(row.OutputEntries.Count, columns.Count); i++)
        {
            var entry = row.OutputEntries[i];
            var column = columns[i];
            if (string.IsNullOrWhiteSpace(entry))
            {
                output[column.Name] = null;
                continue;
            }
            var ast = _evaluator.ParseAndCache(entry);
            output[column.Name] = _evaluator.Evaluate(ast, ctx);
        }
        return output;
    }

    private static string BuildConditionExpression(DecisionInputColumn column, string entry)
    {
        var varExpr = column.Expression ?? column.Name;
        if (entry.StartsWith('<') || entry.StartsWith('>') || entry.StartsWith('!') || entry.StartsWith('='))
            return $"{varExpr} {entry}";
        return $"{varExpr} == {entry}";
    }
}
