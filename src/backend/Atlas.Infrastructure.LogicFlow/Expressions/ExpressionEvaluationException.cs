namespace Atlas.Infrastructure.LogicFlow.Expressions;

/// <summary>
/// RT-14: 表达式求值异常——包含用户友好的错误信息和原始异常链。
/// </summary>
public sealed class ExpressionEvaluationException : Exception
{
    public ExpressionEvaluationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
