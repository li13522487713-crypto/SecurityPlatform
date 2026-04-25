namespace Atlas.Application.AiPlatform.Abstractions;

public interface ISqlSafetyValidator
{
    void ValidateCreateTable(string sql);

    void ValidateCreateView(string sql);

    void ValidateSelectOnly(string sql);
}

public sealed class SqlSafetyException : Exception
{
    public SqlSafetyException(string message)
        : base(message)
    {
    }
}
