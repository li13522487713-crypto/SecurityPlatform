namespace Atlas.Connectors.Core;

/// <summary>
/// 连接层统一异常。承载 provider 级错误码 + 外部原始错误信息，便于 ExceptionHandlingMiddleware 直接映射 ApiResponse。
/// </summary>
public sealed class ConnectorException : Exception
{
    public ConnectorException(string code, string message, string? providerType = null, int? providerErrorCode = null, string? providerMessage = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        ProviderType = providerType;
        ProviderErrorCode = providerErrorCode;
        ProviderMessage = providerMessage;
    }

    public string Code { get; }

    public string? ProviderType { get; }

    public int? ProviderErrorCode { get; }

    public string? ProviderMessage { get; }
}
