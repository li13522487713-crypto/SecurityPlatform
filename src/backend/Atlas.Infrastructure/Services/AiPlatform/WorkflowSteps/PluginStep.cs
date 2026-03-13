using System.Net.Http.Headers;
using System.Text;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.Infrastructure.Services.AiPlatform.WorkflowSteps;

public sealed class PluginStep : StepBodyAsync
{
    private readonly IHttpClientFactory _httpClientFactory;

    public PluginStep(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "POST";
    public string? Headers { get; set; }
    public string? Body { get; set; }
    public string OutputKey { get; set; } = "pluginOutput";

    public override async Task<ExecutionResult> RunAsync(Atlas.WorkflowCore.Abstractions.IStepExecutionContext context)
    {
        var data = WorkflowStepDataHelper.EnsureDataDictionary(context);
        var resolvedUrl = WorkflowStepDataHelper.ResolveTemplate(Url, data);
        if (string.IsNullOrWhiteSpace(resolvedUrl))
        {
            throw new InvalidOperationException("PluginStep requires Url.");
        }

        using var request = new HttpRequestMessage(new HttpMethod(Method), resolvedUrl);
        var resolvedBody = WorkflowStepDataHelper.ResolveTemplate(Body, data);
        if (!string.IsNullOrWhiteSpace(resolvedBody))
        {
            request.Content = new StringContent(resolvedBody, Encoding.UTF8, "application/json");
        }

        ApplyHeaders(request, Headers, data);
        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, context.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(context.CancellationToken);
        data[OutputKey] = content;
        data[$"{OutputKey}StatusCode"] = (int)response.StatusCode;
        return ExecutionResult.Next();
    }

    private static void ApplyHeaders(HttpRequestMessage request, string? rawHeaders, IReadOnlyDictionary<string, object?> data)
    {
        if (string.IsNullOrWhiteSpace(rawHeaders))
        {
            return;
        }

        var resolved = WorkflowStepDataHelper.ResolveTemplate(rawHeaders, data);
        foreach (var segment in resolved.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = segment.Split(':', 2);
            if (pair.Length != 2)
            {
                continue;
            }

            var key = pair[0].Trim();
            var value = pair[1].Trim();
            if (string.Equals(key, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(value);
                continue;
            }

            request.Headers.TryAddWithoutValidation(key, value);
        }
    }
}
