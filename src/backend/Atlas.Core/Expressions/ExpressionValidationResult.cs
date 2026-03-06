namespace Atlas.Core.Expressions;

/// <summary>
/// 表达式静态校验结果
/// </summary>
public sealed class ExpressionValidationResult
{
    public static ExpressionValidationResult Ok() => new(true, [], []);

    public static ExpressionValidationResult Failure(IReadOnlyList<string> errors, IReadOnlyList<string>? warnings = null)
        => new(false, errors, warnings ?? []);

    private ExpressionValidationResult(bool isValid, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
    {
        IsValid = isValid;
        Errors = errors;
        Warnings = warnings;
    }

    /// <summary>是否通过校验</summary>
    public bool IsValid { get; }

    /// <summary>错误信息列表</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>警告信息列表</summary>
    public IReadOnlyList<string> Warnings { get; }
}
