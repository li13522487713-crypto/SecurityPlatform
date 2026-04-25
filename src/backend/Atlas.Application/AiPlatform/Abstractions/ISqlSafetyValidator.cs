namespace Atlas.Application.AiPlatform.Abstractions;

public interface ISqlSafetyValidator
{
    void ValidateCreateTable(string sql);

    void ValidateCreateView(string sql);

    void ValidateSelectOnly(string sql);

    IReadOnlyList<string> SplitStatementsSafely(string sql);

    bool ContainsForbiddenKeyword(string sql);
}

public sealed class SqlSafetyException : Exception
{
    public SqlSafetyException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public SqlSafetyException(string message)
        : this("SQL_SAFETY_VALIDATION_FAILED", message)
    {
    }

    public string Code { get; }
}
