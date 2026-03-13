using System.Text;
using Atlas.WorkflowCore.Models;
using Atlas.WorkflowCore.Primitives;

namespace Atlas.Infrastructure.Services.AiPlatform.WorkflowSteps;

public sealed class HttpRequesterStep : StepBodyAsync
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpRequesterStep(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public string? Body { get; set; }
    public string OutputKey { get; set; } = "httpOutput";

    public override async Task<ExecutionResult> RunAsync(Atlas.WorkflowCore.Abstractions.IStepExecutionContext context)
    {
        var data = WorkflowStepDataHelper.EnsureDataDictionary(context);
        var resolvedUrl = WorkflowStepDataHelper.ResolveTemplate(Url, data);
        if (string.IsNullOrWhiteSpace(resolvedUrl))
        {
            throw new InvalidOperationException("HttpRequesterStep requires Url.");
        }

        using var request = new HttpRequestMessage(new HttpMethod(Method), resolvedUrl);
        var resolvedBody = WorkflowStepDataHelper.ResolveTemplate(Body, data);
        if (!string.IsNullOrWhiteSpace(resolvedBody))
        {
            request.Content = new StringContent(resolvedBody, Encoding.UTF8, "application/json");
        }

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, context.CancellationToken);
        var payload = await response.Content.ReadAsStringAsync(context.CancellationToken);
        data[OutputKey] = payload;
        data[$"{OutputKey}StatusCode"] = (int)response.StatusCode;
        return ExecutionResult.Next();
    }
}
