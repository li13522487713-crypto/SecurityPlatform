using Atlas.Core.Expressions;

namespace Atlas.Infrastructure.LogicFlow.Expressions.Rules;

/// <summary>
/// 规则链执行器 —— 按优先级顺序评估条件表达式，返回首个命中的输出。
/// </summary>
public sealed class RuleChainExecutor : IRuleChainExecutor
{
    private readonly ExprEvaluator _evaluator;

    public RuleChainExecutor(ExprEvaluator evaluator)
    {
        _evaluator = evaluator;
    }

    public RuleChainResult Execute(RuleChainModel chain, IReadOnlyDictionary<string, object?> input)
    {
        var ctx = ExpressionContext.FromRecord(input);

        var steps = chain.Steps.OrderBy(s => s.Priority).ToList();
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var condAst = _evaluator.ParseAndCache(step.ConditionExpression);
            var condResult = _evaluator.Evaluate(condAst, ctx);

            if (condResult is true)
            {
                var outAst = _evaluator.ParseAndCache(step.OutputExpression);
                var output = _evaluator.Evaluate(outAst, ctx);
                return RuleChainResult.Matched(output, i);
            }
        }

        if (!string.IsNullOrWhiteSpace(chain.DefaultOutputExpression))
        {
            var defaultAst = _evaluator.ParseAndCache(chain.DefaultOutputExpression);
            var defaultOutput = _evaluator.Evaluate(defaultAst, ctx);
            return RuleChainResult.Matched(defaultOutput, -1);
        }

        return RuleChainResult.NoMatch();
    }
}
