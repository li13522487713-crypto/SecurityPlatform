using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.Actions.Http;

public static class MicroflowRestBodyKind
{
    public const string None = "none";
    public const string Json = "json";
    public const string Text = "text";
    public const string Form = "form";
    public const string Mapping = "mapping";
}

public static class MicroflowRestResponseHandlingKind
{
    public const string Ignore = "ignore";
    public const string String = "string";
    public const string Json = "json";
    public const string ImportMapping = "importMapping";
}

public static class MicroflowRuntimeHttpErrorKind
{
    public const string None = "none";
    public const string Network = "network";
    public const string Timeout = "timeout";
    public const string Cancelled = "cancelled";
    public const string SecurityBlocked = "securityBlocked";
    public const string ResponseTooLarge = "responseTooLarge";
    public const string Unsupported = "unsupported";
}

public sealed record MicroflowRestExecutionOptions
{
    public bool AllowRealHttp { get; init; }
    public bool AllowPrivateNetwork { get; init; }
    public IReadOnlyList<string> AllowedHosts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DeniedHosts { get; init; } = Array.Empty<string>();
    public int MaxResponseBytes { get; init; } = 256 * 1024;
    public int TimeoutSecondsDefault { get; init; } = 15;
    public IReadOnlyList<string> RedactHeaders { get; init; } =
    [
        "authorization",
        "cookie",
        "set-cookie",
        "x-api-key",
        "api-key",
        "apikey",
        "proxy-authorization"
    ];
    public bool FollowRedirects { get; init; }
    public int MaxRedirects { get; init; } = 3;
    public bool TreatNonSuccessStatusAsError { get; init; } = true;
    public int MockResponseStatusCode { get; init; } = 200;
    public JsonElement? MockResponseBodyJson { get; init; }
    public string? MockResponseBodyText { get; init; } = "{\"mock\":true,\"source\":\"microflow-rest\"}";
    public int MaxUrlLength { get; init; } = 2048;
    public int MaxHeaderValueLength { get; init; } = 4096;
    public bool LogExpressionErrorsAsWarning { get; init; }

    public MicroflowRuntimeHttpOptions ToHttpOptions(bool? allowRealHttpOverride = null)
        => new()
        {
            AllowRealHttp = allowRealHttpOverride ?? AllowRealHttp,
            AllowPrivateNetwork = AllowPrivateNetwork,
            AllowedHosts = AllowedHosts,
            DeniedHosts = DeniedHosts,
            MaxResponseBytes = MaxResponseBytes,
            TimeoutSecondsDefault = TimeoutSecondsDefault,
            RedactHeaders = RedactHeaders,
            FollowRedirects = FollowRedirects,
            MaxRedirects = MaxRedirects,
            TreatNonSuccessStatusAsError = TreatNonSuccessStatusAsError,
            MockResponseStatusCode = MockResponseStatusCode,
            MockResponseBodyJson = MockResponseBodyJson,
            MockResponseBodyText = MockResponseBodyText,
            MaxUrlLength = MaxUrlLength,
            MaxHeaderValueLength = MaxHeaderValueLength
        };
}

public sealed record MicroflowRuntimeHttpOptions
{
    public bool AllowRealHttp { get; init; }
    public bool AllowPrivateNetwork { get; init; }
    public IReadOnlyList<string> AllowedHosts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DeniedHosts { get; init; } = Array.Empty<string>();
    public int MaxResponseBytes { get; init; } = 256 * 1024;
    public int TimeoutSecondsDefault { get; init; } = 15;
    public IReadOnlyList<string> RedactHeaders { get; init; } = Array.Empty<string>();
    public bool FollowRedirects { get; init; }
    public int MaxRedirects { get; init; } = 3;
    public bool TreatNonSuccessStatusAsError { get; init; } = true;
    public int MockResponseStatusCode { get; init; } = 200;
    public JsonElement? MockResponseBodyJson { get; init; }
    public string? MockResponseBodyText { get; init; } = "{\"mock\":true}";
    public int MaxUrlLength { get; init; } = 2048;
    public int MaxHeaderValueLength { get; init; } = 4096;
}

public sealed record MicroflowRuntimeHttpRequest
{
    public string Method { get; init; } = "GET";
    public string Url { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string> QueryParameters { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
    public string BodyKind { get; init; } = MicroflowRestBodyKind.None;
    public string? BodyText { get; init; }
    public JsonElement? BodyJson { get; init; }
    public IReadOnlyDictionary<string, string> FormFields { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
    public int? TimeoutSeconds { get; init; }
    public string? TraceId { get; init; }
    public string? SourceObjectId { get; init; }
    public string? SourceActionId { get; init; }
}

public sealed record MicroflowRuntimeHttpError
{
    public string Kind { get; init; } = MicroflowRuntimeHttpErrorKind.None;
    public string Code { get; init; } = RuntimeErrorCode.RuntimeRestCallFailed;
    public string Message { get; init; } = string.Empty;
    public string? Details { get; init; }
}

public sealed record MicroflowRuntimeHttpResponse
{
    public bool Success { get; init; }
    public int? StatusCode { get; init; }
    public string? ReasonPhrase { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public string? BodyText { get; init; }
    public JsonElement? BodyJson { get; init; }
    public string? BodyPreview { get; init; }
    public int DurationMs { get; init; }
    public MicroflowRuntimeHttpError? Error { get; init; }
    public bool Truncated { get; init; }
    public string? ContentType { get; init; }
    public long? ContentLength { get; init; }
}

public sealed record MicroflowRestRequestPreview
{
    [JsonPropertyName("method")]
    public string Method { get; init; } = "GET";

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("headers")]
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("query")]
    public IReadOnlyDictionary<string, string> Query { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    [JsonPropertyName("bodyKind")]
    public string BodyKind { get; init; } = MicroflowRestBodyKind.None;

    [JsonPropertyName("bodyPreview")]
    public string? BodyPreview { get; init; }

    [JsonPropertyName("externalRequestSent")]
    public bool ExternalRequestSent { get; init; }
}

public sealed record MicroflowRestRequestBuildResult
{
    public bool Success { get; init; }
    public MicroflowRuntimeHttpRequest? Request { get; init; }
    public MicroflowRestRequestPreview? RequestPreview { get; init; }
    public IReadOnlyList<MicroflowActionExecutionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowActionExecutionDiagnostic>();
    public MicroflowRuntimeErrorDto? Error { get; init; }
}

public sealed record MicroflowRestResponseHandleResult
{
    public bool Success { get; init; }
    public JsonElement OutputJson { get; init; }
    public string OutputPreview { get; init; } = string.Empty;
    public IReadOnlyList<MicroflowRuntimeVariableValueDto> ProducedVariables { get; init; } = Array.Empty<MicroflowRuntimeVariableValueDto>();
    public IReadOnlyList<MicroflowActionExecutionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowActionExecutionDiagnostic>();
    public MicroflowRuntimeErrorDto? Error { get; init; }
}

public sealed record MicroflowRestExecutionResult
{
    public bool Success { get; init; }
    public MicroflowRuntimeHttpRequest? Request { get; init; }
    public MicroflowRuntimeHttpResponse? Response { get; init; }
    public MicroflowRestRequestPreview? RequestPreview { get; init; }
    public JsonElement? LatestHttpResponse { get; init; }
    public MicroflowRuntimeErrorDto? Error { get; init; }
    public IReadOnlyList<MicroflowRuntimeVariableValueDto> ProducedVariables { get; init; } = Array.Empty<MicroflowRuntimeVariableValueDto>();
}
