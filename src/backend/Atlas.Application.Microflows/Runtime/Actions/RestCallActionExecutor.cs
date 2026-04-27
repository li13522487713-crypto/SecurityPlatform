using System.Diagnostics;
using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Actions.Http;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Microflows.Runtime.Actions;

public sealed class RestCallActionExecutor : IMicroflowActionExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly MicroflowRestRequestBuilder _requestBuilder;
    private readonly MicroflowRestSecurityPolicy _securityPolicy;
    private readonly IMicroflowRuntimeHttpClient _httpClient;
    private readonly MicroflowRestResponseHandler _responseHandler;
    private readonly MicroflowRestExecutionOptions _defaultOptions;
    private readonly ILogger<RestCallActionExecutor>? _logger;

    public RestCallActionExecutor(
        MicroflowRestRequestBuilder requestBuilder,
        MicroflowRestSecurityPolicy securityPolicy,
        IMicroflowRuntimeHttpClient httpClient,
        MicroflowRestResponseHandler responseHandler,
        MicroflowRestExecutionOptions defaultOptions,
        ILogger<RestCallActionExecutor>? logger = null)
    {
        _requestBuilder = requestBuilder;
        _securityPolicy = securityPolicy;
        _httpClient = httpClient;
        _responseHandler = responseHandler;
        _defaultOptions = defaultOptions;
        _logger = logger;
    }

    public string ActionKind => "restCall";

    public string Category => MicroflowActionRuntimeCategory.ServerExecutable;

    public string SupportLevel => MicroflowActionSupportLevel.Supported;

    public async Task<MicroflowActionExecutionResult> ExecuteAsync(
        MicroflowActionExecutionContext context,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var options = BuildOptions(context);
        var build = await _requestBuilder.BuildAsync(context, options, ct);
        if (!build.Success || build.Request is null)
        {
            return Failed(context, build.Error ?? Error(context, RuntimeErrorCode.RuntimeRestInvalidUrl, "RestCall request build failed."), stopwatch, build.RequestPreview, null, null, build.Diagnostics);
        }

        var request = build.Request;
        var securityDecision = await _securityPolicy.EvaluateAsync(request, options, resolveHostAddresses: options.AllowRealHttp, ct);
        if (!securityDecision.Allowed)
        {
            var latest = LatestHttpResponse(request, null, MicroflowRuntimeHttpErrorKind.SecurityBlocked, securityDecision.Message);
            return Failed(
                context,
                Error(context, securityDecision.ReasonCode, securityDecision.Message, details: JsonSerializer.Serialize(new { restCall = new { securityDecision } }, JsonOptions)),
                stopwatch,
                build.RequestPreview,
                null,
                latest,
                build.Diagnostics,
                securityDecision);
        }

        if (context.Options.SimulateRestError)
        {
            var simulated = new MicroflowRuntimeHttpResponse
            {
                Success = false,
                StatusCode = 500,
                ReasonPhrase = "Simulated",
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["content-type"] = "application/json" },
                BodyText = "{\"error\":\"mock-rest-error\"}",
                BodyPreview = "mock-rest-error",
                Error = new MicroflowRuntimeHttpError
                {
                    Code = RuntimeErrorCode.RuntimeRestCallFailed,
                    Kind = MicroflowRuntimeHttpErrorKind.Network,
                    Message = "Mock REST call failed by simulateRestError."
                }
            };
            var latest = LatestHttpResponse(request, simulated, MicroflowRuntimeHttpErrorKind.Network, simulated.Error.Message);
            return Failed(context, Error(context, RuntimeErrorCode.RuntimeRestCallFailed, simulated.Error.Message), stopwatch, build.RequestPreview, simulated, latest, build.Diagnostics, securityDecision);
        }

        var response = await _httpClient.SendAsync(request, options, ct);
        if (response.Error is not null && response.Error.Kind is MicroflowRuntimeHttpErrorKind.Network or MicroflowRuntimeHttpErrorKind.Timeout or MicroflowRuntimeHttpErrorKind.Cancelled)
        {
            var latest = LatestHttpResponse(request, response, response.Error.Kind, response.Error.Message);
            var code = response.Error.Kind == MicroflowRuntimeHttpErrorKind.Timeout ? RuntimeErrorCode.RuntimeRestTimeout : response.Error.Code;
            return Failed(context, Error(context, code, response.Error.Message, response.Error.Details), stopwatch, build.RequestPreview, response, latest, build.Diagnostics, securityDecision);
        }

        if (!response.Success && options.TreatNonSuccessStatusAsError)
        {
            var latest = LatestHttpResponse(request, response, "httpStatus", $"REST call returned HTTP {response.StatusCode}.");
            return Failed(
                context,
                Error(context, RuntimeErrorCode.RuntimeRestCallFailed, $"REST call returned HTTP {response.StatusCode} {response.ReasonPhrase}.", response.BodyPreview),
                stopwatch,
                build.RequestPreview,
                response,
                latest,
                build.Diagnostics,
                securityDecision);
        }

        var handled = await _responseHandler.HandleAsync(context, request, response, options, ct);
        stopwatch.Stop();
        if (!handled.Success)
        {
            var latest = LatestHttpResponse(request, response, "responseHandling", handled.Error?.Message);
            return Failed(context, handled.Error ?? Error(context, RuntimeErrorCode.RuntimeRestCallFailed, "RestCall response handling failed."), stopwatch, build.RequestPreview, response, latest, handled.Diagnostics, securityDecision, handled.ProducedVariables);
        }

        var output = MergeRestOutput(handled.OutputJson, build.RequestPreview, response, options, securityDecision, handled.ProducedVariables);
        var logs = CreateLogs(context, request, response, build.RequestPreview, securityDecision, error: null);
        _logger?.LogDebug("Microflow RestCall {ObjectId}/{ActionId} completed with HTTP {StatusCode}.", context.ObjectId, context.ActionId, response.StatusCode);
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = output,
            OutputPreview = handled.OutputPreview,
            ProducedVariables = handled.ProducedVariables,
            Logs = logs,
            Diagnostics = build.Diagnostics.Concat(handled.Diagnostics).ToArray(),
            DurationMs = (int)stopwatch.ElapsedMilliseconds,
            ShouldContinueNormalFlow = true,
            Message = $"REST {request.Method} {response.StatusCode}"
        };
    }

    private MicroflowRuntimeHttpOptions BuildOptions(MicroflowActionExecutionContext context)
    {
        var allowRealHttp = context.Options.AllowRealHttp || ReadBool(context.ActionConfig, "allowRealHttp");
        var options = _defaultOptions.ToHttpOptions(allowRealHttp);
        if (ReadInt(context.ActionConfig, "timeoutSeconds") is { } timeout && timeout > 0)
        {
            options = options with { TimeoutSecondsDefault = timeout };
        }

        return options;
    }

    private MicroflowActionExecutionResult Failed(
        MicroflowActionExecutionContext context,
        MicroflowRuntimeErrorDto error,
        Stopwatch stopwatch,
        MicroflowRestRequestPreview? requestPreview,
        MicroflowRuntimeHttpResponse? response,
        JsonElement? latestHttpResponse,
        IReadOnlyList<MicroflowActionExecutionDiagnostic> diagnostics,
        MicroflowRestSecurityDecision? securityDecision = null,
        IReadOnlyList<MicroflowRuntimeVariableValueDto>? producedVariables = null)
    {
        stopwatch.Stop();
        var output = JsonSerializer.SerializeToElement(new
        {
            restCall = new
            {
                requestPreview,
                allowRealHttp = BuildOptions(context).AllowRealHttp,
                securityDecision,
                statusCode = response?.StatusCode,
                reasonPhrase = response?.ReasonPhrase,
                responsePreview = response?.BodyPreview,
                responseHandling = "failed",
                durationMs = response?.DurationMs ?? (int)stopwatch.ElapsedMilliseconds,
                truncated = response?.Truncated ?? false,
                producedVariables = producedVariables?.Select(variable => variable.Name).ToArray() ?? Array.Empty<string>(),
                error = error
            }
        }, JsonOptions);
        var logs = CreateLogs(context, null, response, requestPreview, securityDecision, error);
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Failed,
            OutputJson = output,
            OutputPreview = error.Message,
            Error = error,
            LatestHttpResponse = latestHttpResponse,
            ProducedVariables = producedVariables ?? Array.Empty<MicroflowRuntimeVariableValueDto>(),
            Logs = logs,
            Diagnostics = diagnostics,
            DurationMs = (int)stopwatch.ElapsedMilliseconds,
            ShouldContinueNormalFlow = false,
            ShouldEnterErrorHandler = true,
            ShouldStopRun = true,
            Message = error.Message
        };
    }

    private static MicroflowRuntimeErrorDto Error(
        MicroflowActionExecutionContext context,
        string code,
        string message,
        string? details = null)
        => new()
        {
            Code = code,
            Message = message,
            ObjectId = context.ObjectId,
            ActionId = context.ActionId,
            Details = details
        };

    private static JsonElement MergeRestOutput(
        JsonElement handledOutput,
        MicroflowRestRequestPreview? requestPreview,
        MicroflowRuntimeHttpResponse response,
        MicroflowRuntimeHttpOptions options,
        MicroflowRestSecurityDecision securityDecision,
        IReadOnlyList<MicroflowRuntimeVariableValueDto> producedVariables)
    {
        var restCall = new
        {
            method = requestPreview?.Method,
            urlPreview = requestPreview?.Url,
            headersPreview = requestPreview?.Headers,
            queryPreview = requestPreview?.Query,
            bodyPreview = requestPreview?.BodyPreview,
            allowRealHttp = options.AllowRealHttp,
            securityDecision,
            statusCode = response.StatusCode,
            responsePreview = response.BodyPreview,
            responseHandling = ReadResponseHandling(handledOutput),
            producedVariables = producedVariables.Select(variable => variable.Name).ToArray(),
            response.DurationMs,
            response.Truncated,
            connectorCapabilities = new
            {
                restRealHttp = MicroflowRuntimeConnectorCapability.RestRealHttp,
                restImportMapping = MicroflowRuntimeConnectorCapability.RestImportMapping,
                restExportMapping = MicroflowRuntimeConnectorCapability.RestExportMapping
            }
        };

        return JsonSerializer.SerializeToElement(new { restCall, handled = handledOutput }, JsonOptions);
    }

    private static string ReadResponseHandling(JsonElement handledOutput)
        => handledOutput.ValueKind == JsonValueKind.Object
            && handledOutput.TryGetProperty("restCall", out var restCall)
            && restCall.ValueKind == JsonValueKind.Object
            && restCall.TryGetProperty("responseHandling", out var handling)
            && handling.ValueKind == JsonValueKind.String
                ? handling.GetString() ?? MicroflowRestResponseHandlingKind.Ignore
                : MicroflowRestResponseHandlingKind.Ignore;

    private static JsonElement LatestHttpResponse(
        MicroflowRuntimeHttpRequest request,
        MicroflowRuntimeHttpResponse? response,
        string errorKind,
        string? message)
        => JsonSerializer.SerializeToElement(new
        {
            statusCode = response?.StatusCode,
            reasonPhrase = response?.ReasonPhrase,
            headers = response is null ? null : MicroflowRestRedaction.RedactHeaders(response.Headers, Array.Empty<string>()),
            bodyText = response?.BodyText,
            bodyJson = response?.BodyJson,
            bodyPreview = response?.BodyPreview,
            requestUrl = request.Url,
            method = request.Method,
            durationMs = response?.DurationMs,
            errorKind,
            message,
            timestamp = DateTimeOffset.UtcNow
        }, JsonOptions);

    private static IReadOnlyList<MicroflowRuntimeLogDto> CreateLogs(
        MicroflowActionExecutionContext context,
        MicroflowRuntimeHttpRequest? request,
        MicroflowRuntimeHttpResponse? response,
        MicroflowRestRequestPreview? requestPreview,
        MicroflowRestSecurityDecision? securityDecision,
        MicroflowRuntimeErrorDto? error)
    {
        var fields = JsonSerializer.Serialize(new
        {
            kind = "restCall",
            request = requestPreview,
            securityDecision,
            statusCode = response?.StatusCode,
            reasonPhrase = response?.ReasonPhrase,
            responsePreview = response?.BodyPreview,
            response?.DurationMs,
            response?.Truncated,
            error
        }, JsonOptions);
        var level = error is null ? "info" : "error";
        var message = error is null
            ? $"REST {request?.Method ?? requestPreview?.Method} -> HTTP {response?.StatusCode}"
            : $"REST failed: {error.Code} {error.Message}";
        return
        [
            new MicroflowRuntimeLogDto
            {
                Id = Guid.NewGuid().ToString("N"),
                Timestamp = DateTimeOffset.UtcNow,
                Level = level,
                ObjectId = context.ObjectId,
                ActionId = context.ActionId,
                LogNodeName = "RestCall",
                TraceId = context.RuntimeExecutionContext.RunId,
                Message = message,
                StructuredFieldsJson = fields
            }
        ];
    }

    private static bool ReadBool(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.True;

    private static int? ReadInt(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var result)
                ? result
                : null;
}
