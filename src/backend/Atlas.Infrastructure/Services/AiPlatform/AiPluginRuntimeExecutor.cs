using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Security;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiPluginRuntimeExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AiPluginRuntimeExecutor> _logger;
    private readonly ISecretRefResolver _secretRefResolver;

    public AiPluginRuntimeExecutor(
        IHttpClientFactory httpClientFactory,
        ISecretRefResolver secretRefResolver,
        ILogger<AiPluginRuntimeExecutor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _secretRefResolver = secretRefResolver;
        _logger = logger;
    }

    public async Task<AiPluginRuntimeExecutionResult> ExecuteAsync(
        TenantId tenantId,
        AiPlugin plugin,
        AiPluginApi api,
        string? inputJson,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(api);

        var watch = Stopwatch.StartNew();
        try
        {
            var baseUrl = ResolveBaseUrl(plugin);
            var requestInput = ParseJsonObject(inputJson);
            if (TryExecuteFixture(plugin, requestInput, tenantId, api, watch, out var fixtureResult))
            {
                return fixtureResult;
            }

            var authConfig = ParseJsonObject(plugin.AuthConfigJson);
            ResolveSecretRefs(authConfig);
            var pathParameters = ExtractObject(requestInput, "path");
            var queryParameters = ExtractObject(requestInput, "query");
            var headerParameters = ExtractObject(requestInput, "headers");
            var bodyNode = requestInput["body"];

            var remainingParameters = requestInput
                .Where(pair => !ReservedInputKeys.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value?.DeepClone(), StringComparer.OrdinalIgnoreCase);

            var requestPath = BindPath(api.Path, pathParameters, remainingParameters);
            var queryString = BuildQueryString(queryParameters, remainingParameters, plugin.AuthType, authConfig);
            var requestUri = BuildRequestUri(baseUrl, requestPath, queryString);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(api.TimeoutSeconds > 0 ? api.TimeoutSeconds : 30));

            using var request = new HttpRequestMessage(new HttpMethod(api.Method), requestUri);
            ApplyAuth(request, plugin.AuthType, authConfig);
            ApplyCustomHeaders(request, headerParameters);

            if (bodyNode is not null && api.Method is not "GET" and not "HEAD")
            {
                request.Content = new StringContent(bodyNode.ToJsonString(JsonOptions), Encoding.UTF8, "application/json");
            }

            var client = _httpClientFactory.CreateClient("AiPlatform");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutCts.Token);
            var responseText = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            watch.Stop();

            return new AiPluginRuntimeExecutionResult(
                response.IsSuccessStatusCode,
                JsonSerializer.Serialize(new
                {
                    tenantId = tenantId.Value,
                    pluginId = plugin.Id,
                    pluginName = plugin.Name,
                    apiId = api.Id,
                    apiName = api.Name,
                    request = new
                    {
                        method = api.Method,
                        url = requestUri.ToString()
                    },
                    response = new
                    {
                        statusCode = (int)response.StatusCode,
                        reasonPhrase = response.ReasonPhrase,
                        body = TryParseJson(responseText)
                    }
                }),
                response.IsSuccessStatusCode ? null : $"插件调用返回 {(int)response.StatusCode} {response.ReasonPhrase}",
                watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            watch.Stop();
            return new AiPluginRuntimeExecutionResult(false, "{}", "插件调用超时。", watch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            watch.Stop();
            _logger.LogWarning(
                ex,
                "执行插件失败。tenantId={TenantId}, pluginId={PluginId}, apiId={ApiId}",
                tenantId.Value,
                plugin.Id,
                api.Id);
            return new AiPluginRuntimeExecutionResult(false, "{}", ex.Message, watch.ElapsedMilliseconds);
        }
    }

    private static readonly HashSet<string> ReservedInputKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "body",
        "path",
        "query",
        "headers"
    };

    private void ResolveSecretRefs(JsonObject authConfig)
    {
        foreach (var key in authConfig.Select(item => item.Key).ToArray())
        {
            if (authConfig[key] is JsonObject nested)
            {
                ResolveSecretRefs(nested);
                continue;
            }

            if (authConfig[key] is not JsonValue valueNode)
            {
                continue;
            }

            if (!valueNode.TryGetValue<string>(out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var resolved = _secretRefResolver.Resolve(raw);
            if (!string.Equals(resolved, raw, StringComparison.Ordinal))
            {
                authConfig[key] = resolved;
            }
        }
    }

    private static string ResolveBaseUrl(AiPlugin plugin)
    {
        var definition = ParseJsonObject(plugin.DefinitionJson);
        if (string.Equals(GetString(definition, "fixture"), "echo", StringComparison.OrdinalIgnoreCase))
        {
            return "http://fixture.local";
        }

        var baseUrl =
            GetString(definition, "baseUrl") ??
            GetString(definition, "base_url") ??
            GetString(definition, "url");

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            return baseUrl.Trim();
        }

        var openApi = ParseJsonObject(plugin.OpenApiSpecJson);
        if (openApi["servers"] is JsonArray servers)
        {
            foreach (var node in servers)
            {
                if (node is JsonObject serverObject)
                {
                    baseUrl = GetString(serverObject, "url");
                    if (!string.IsNullOrWhiteSpace(baseUrl))
                    {
                        return baseUrl.Trim();
                    }
                }
            }
        }

        throw new InvalidOperationException($"插件「{plugin.Name}」缺少可用的 baseUrl。");
    }

    private static bool TryExecuteFixture(
        AiPlugin plugin,
        JsonObject requestInput,
        TenantId tenantId,
        AiPluginApi api,
        Stopwatch watch,
        out AiPluginRuntimeExecutionResult result)
    {
        result = default!;
        var definition = ParseJsonObject(plugin.DefinitionJson);
        if (!string.Equals(GetString(definition, "fixture"), "echo", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        watch.Stop();
        result = new AiPluginRuntimeExecutionResult(
            true,
            JsonSerializer.Serialize(new
            {
                tenantId = tenantId.Value,
                pluginId = plugin.Id,
                pluginName = plugin.Name,
                apiId = api.Id,
                apiName = api.Name,
                fixture = "echo",
                input = requestInput
            }, JsonOptions),
            null,
            watch.ElapsedMilliseconds);
        return true;
    }

    private static string BindPath(
        string pathTemplate,
        JsonObject pathParameters,
        IDictionary<string, JsonNode?> remainingParameters)
    {
        var boundPath = pathTemplate;
        foreach (var token in EnumeratePathTokens(pathTemplate))
        {
            var value =
                GetString(pathParameters, token) ??
                GetString(remainingParameters, token);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            boundPath = boundPath.Replace($"{{{token}}}", Uri.EscapeDataString(value), StringComparison.OrdinalIgnoreCase);
            remainingParameters.Remove(token);
        }

        return boundPath;
    }

    private static string BuildQueryString(
        JsonObject queryParameters,
        IDictionary<string, JsonNode?> remainingParameters,
        AiPluginAuthType authType,
        JsonObject authConfig)
    {
        var pairs = new List<string>();
        AppendQueryPairs(pairs, queryParameters);

        foreach (var pair in remainingParameters)
        {
            AppendQueryPair(pairs, pair.Key, pair.Value);
        }

        if (authType == AiPluginAuthType.ApiKey)
        {
            var apiKeyIn = GetString(authConfig, "in") ?? GetString(authConfig, "location");
            if (string.Equals(apiKeyIn, "query", StringComparison.OrdinalIgnoreCase))
            {
                var keyName =
                    GetString(authConfig, "headerName") ??
                    GetString(authConfig, "keyName") ??
                    GetString(authConfig, "name") ??
                    "api_key";
                var keyValue =
                    GetString(authConfig, "apiKey") ??
                    GetString(authConfig, "value");
                if (!string.IsNullOrWhiteSpace(keyValue))
                {
                    pairs.Add($"{Uri.EscapeDataString(keyName)}={Uri.EscapeDataString(keyValue)}");
                }
            }
        }

        return pairs.Count == 0 ? string.Empty : string.Join("&", pairs);
    }

    private static Uri BuildRequestUri(string baseUrl, string path, string queryString)
    {
        var baseUri = new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        var relative = path.TrimStart('/');
        if (!string.IsNullOrWhiteSpace(queryString))
        {
            relative = $"{relative}?{queryString}";
        }

        return new Uri(baseUri, relative);
    }

    private static void ApplyAuth(HttpRequestMessage request, AiPluginAuthType authType, JsonObject authConfig)
    {
        switch (authType)
        {
            case AiPluginAuthType.ApiKey:
            {
                var apiKeyIn = GetString(authConfig, "in") ?? GetString(authConfig, "location");
                if (string.Equals(apiKeyIn, "query", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var keyName =
                    GetString(authConfig, "headerName") ??
                    GetString(authConfig, "keyName") ??
                    GetString(authConfig, "name") ??
                    "X-API-Key";
                var keyValue =
                    GetString(authConfig, "apiKey") ??
                    GetString(authConfig, "value");
                if (!string.IsNullOrWhiteSpace(keyValue))
                {
                    request.Headers.TryAddWithoutValidation(keyName, keyValue);
                }

                break;
            }
            case AiPluginAuthType.BearerToken:
            {
                var token =
                    GetString(authConfig, "bearerToken") ??
                    GetString(authConfig, "token") ??
                    GetString(authConfig, "accessToken");
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                break;
            }
            case AiPluginAuthType.Basic:
            {
                var username = GetString(authConfig, "username") ?? string.Empty;
                var password = GetString(authConfig, "password") ?? string.Empty;
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                break;
            }
            case AiPluginAuthType.Custom:
                ApplyCustomHeaders(request, ExtractObject(authConfig, "headers"));
                break;
        }
    }

    private static void ApplyCustomHeaders(HttpRequestMessage request, JsonObject headerParameters)
    {
        foreach (var pair in headerParameters)
        {
            var value = NodeToString(pair.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            request.Headers.TryAddWithoutValidation(pair.Key, value);
        }
    }

    private static IEnumerable<string> EnumeratePathTokens(string pathTemplate)
    {
        var startIndex = 0;
        while (startIndex < pathTemplate.Length)
        {
            var openIndex = pathTemplate.IndexOf('{', startIndex);
            if (openIndex < 0)
            {
                yield break;
            }

            var closeIndex = pathTemplate.IndexOf('}', openIndex + 1);
            if (closeIndex < 0)
            {
                yield break;
            }

            var token = pathTemplate[(openIndex + 1)..closeIndex].Trim();
            if (!string.IsNullOrWhiteSpace(token))
            {
                yield return token;
            }

            startIndex = closeIndex + 1;
        }
    }

    private static void AppendQueryPairs(List<string> pairs, JsonObject values)
    {
        foreach (var pair in values)
        {
            AppendQueryPair(pairs, pair.Key, pair.Value);
        }
    }

    private static void AppendQueryPair(List<string> pairs, string key, JsonNode? value)
    {
        if (value is null)
        {
            return;
        }

        if (value is JsonArray array)
        {
            foreach (var item in array)
            {
                AppendQueryPair(pairs, key, item);
            }

            return;
        }

        var text = NodeToString(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        pairs.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(text)}");
    }

    private static JsonObject ParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static JsonObject ExtractObject(JsonObject source, string propertyName)
    {
        return source[propertyName] as JsonObject ?? new JsonObject();
    }

    private static string? GetString(JsonObject source, string propertyName)
        => source.TryGetPropertyValue(propertyName, out var node) ? NodeToString(node) : null;

    private static string? GetString(IDictionary<string, JsonNode?> source, string propertyName)
        => source.TryGetValue(propertyName, out var node) ? NodeToString(node) : null;

    private static string? NodeToString(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue valueNode)
        {
            if (valueNode.TryGetValue<string>(out var stringValue))
            {
                return stringValue;
            }

            return valueNode.ToJsonString().Trim('"');
        }

        return node.ToJsonString();
    }

    private static object? TryParseJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(text);
        }
        catch (JsonException)
        {
            return text;
        }
    }
}

public sealed record AiPluginRuntimeExecutionResult(
    bool Success,
    string OutputJson,
    string? ErrorMessage,
    long DurationMs);
