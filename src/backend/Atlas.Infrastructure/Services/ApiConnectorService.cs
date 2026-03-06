using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Atlas.Application.Integration;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Integration;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class ApiConnectorService : IApiConnectorService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly ITenantProvider _tenantProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiConnectorService> _logger;

    public ApiConnectorService(
        ISqlSugarClient db,
        IIdGeneratorAccessor idGen,
        ITenantProvider tenantProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<ApiConnectorService> logger)
    {
        _db = db;
        _idGen = idGen;
        _tenantProvider = tenantProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ApiConnector>> GetAllAsync(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        return await _db.Queryable<ApiConnector>()
            .Where(c => c.TenantIdValue == tenantId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ApiConnector?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        return await _db.Queryable<ApiConnector>()
            .Where(c => c.Id == id && c.TenantIdValue == tenantId)
            .FirstAsync(cancellationToken);
    }

    public async Task<long> CreateAsync(CreateApiConnectorRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var connector = new ApiConnector
        {
            Id = _idGen.Generator.NextId(),
            TenantId = _tenantProvider.TenantId.Value,
            Name = request.Name,
            BaseUrl = request.BaseUrl.TrimEnd('/'),
            AuthType = request.AuthType,
            AuthConfig = request.AuthConfig,
            OpenApiSpecUrl = request.OpenApiSpecUrl,
            HealthCheckUrl = request.HealthCheckUrl,
            TimeoutSeconds = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 30,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _db.Insertable(connector).ExecuteCommandAsync(cancellationToken);
        return connector.Id;
    }

    public async Task UpdateAsync(long id, UpdateApiConnectorRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        await _db.Updateable<ApiConnector>()
            .SetColumns(c => new ApiConnector
            {
                Name = request.Name,
                BaseUrl = request.BaseUrl.TrimEnd('/'),
                AuthType = request.AuthType,
                AuthConfig = request.AuthConfig,
                OpenApiSpecUrl = request.OpenApiSpecUrl,
                HealthCheckUrl = request.HealthCheckUrl,
                TimeoutSeconds = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 30,
                IsActive = request.IsActive,
                UpdatedAt = DateTimeOffset.UtcNow
            })
            .Where(c => c.Id == id && c.TenantIdValue == tenantId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        await _db.Deleteable<ApiConnector>().Where(c => c.Id == id && c.TenantIdValue == tenantId).ExecuteCommandAsync(cancellationToken);
        await _db.Deleteable<ApiConnectorOperation>().Where(o => o.ConnectorId == id).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApiConnectorOperation>> GetOperationsAsync(long connectorId, CancellationToken cancellationToken)
    {
        return await _db.Queryable<ApiConnectorOperation>()
            .Where(o => o.ConnectorId == connectorId)
            .ToListAsync(cancellationToken);
    }

    public async Task SyncFromSpecAsync(long connectorId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        var connector = await _db.Queryable<ApiConnector>()
            .Where(c => c.Id == connectorId && c.TenantIdValue == tenantId)
            .FirstAsync(cancellationToken);

        if (connector?.OpenApiSpecUrl is null) return;

        var client = _httpClientFactory.CreateClient();
        var specJson = await client.GetStringAsync(connector.OpenApiSpecUrl, cancellationToken);

        var operations = ParseOpenApiSpec(connectorId, specJson);

        // 删除旧操作，批量插入新操作
        await _db.Deleteable<ApiConnectorOperation>().Where(o => o.ConnectorId == connectorId).ExecuteCommandAsync(cancellationToken);
        if (operations.Count > 0)
        {
            await _db.Insertable(operations).ExecuteCommandAsync(cancellationToken);
        }

        _logger.LogInformation("Synced {Count} operations for connector {Id}", operations.Count, connectorId);
    }

    public async Task<ApiConnectorExecuteResult> ExecuteAsync(
        long connectorId,
        string operationId,
        Dictionary<string, string?> pathParams,
        Dictionary<string, string?> queryParams,
        string? requestBody,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        var connector = await _db.Queryable<ApiConnector>()
            .Where(c => c.Id == connectorId && c.TenantIdValue == tenantId)
            .FirstAsync(cancellationToken);

        if (connector is null)
        {
            return new ApiConnectorExecuteResult(false, 0, "Connector not found", 0);
        }

        var operation = await _db.Queryable<ApiConnectorOperation>()
            .Where(o => o.ConnectorId == connectorId && o.OperationId == operationId)
            .FirstAsync(cancellationToken);

        if (operation is null)
        {
            return new ApiConnectorExecuteResult(false, 0, "Operation not found", 0);
        }

        var path = operation.Path;
        foreach (var (k, v) in pathParams)
        {
            path = path.Replace($"{{{k}}}", Uri.EscapeDataString(v ?? string.Empty));
        }

        var url = $"{connector.BaseUrl}{path}";
        if (queryParams.Count > 0)
        {
            var query = string.Join("&", queryParams.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value ?? string.Empty)}"));
            url = $"{url}?{query}";
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(connector.TimeoutSeconds);
        ApplyAuth(client, connector);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        HttpResponseMessage response;
        var method = new HttpMethod(operation.Method.ToUpperInvariant());
        var request = new HttpRequestMessage(method, url);

        if (!string.IsNullOrEmpty(requestBody))
        {
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        }

        response = await client.SendAsync(request, cancellationToken);
        sw.Stop();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new ApiConnectorExecuteResult((int)response.StatusCode >= 200 && (int)response.StatusCode < 300, (int)response.StatusCode, body, sw.ElapsedMilliseconds);
    }

    public async Task<bool> HealthCheckAsync(long connectorId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId.Value;
        var connector = await _db.Queryable<ApiConnector>()
            .Where(c => c.Id == connectorId && c.TenantIdValue == tenantId)
            .FirstAsync(cancellationToken);

        if (connector is null || string.IsNullOrEmpty(connector.HealthCheckUrl)) return false;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var response = await client.GetAsync(connector.HealthCheckUrl, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyAuth(HttpClient client, ApiConnector connector)
    {
        if (connector.AuthType == ApiAuthType.None || string.IsNullOrEmpty(connector.AuthConfig)) return;

        var config = JsonSerializer.Deserialize<Dictionary<string, string>>(connector.AuthConfig) ?? new();
        switch (connector.AuthType)
        {
            case ApiAuthType.Bearer:
                if (config.TryGetValue("token", out var token))
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                break;
            case ApiAuthType.ApiKey:
                if (config.TryGetValue("header", out var header) && config.TryGetValue("value", out var value))
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header, value);
                break;
            case ApiAuthType.BasicAuth:
                if (config.TryGetValue("username", out var user) && config.TryGetValue("password", out var pass))
                {
                    var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encoded);
                }
                break;
        }
    }

    private List<ApiConnectorOperation> ParseOpenApiSpec(long connectorId, string specJson)
    {
        var operations = new List<ApiConnectorOperation>();
        try
        {
            using var doc = JsonDocument.Parse(specJson);
            if (!doc.RootElement.TryGetProperty("paths", out var paths)) return operations;

            foreach (var pathProp in paths.EnumerateObject())
            {
                foreach (var methodProp in pathProp.Value.EnumerateObject())
                {
                    var operationId = methodProp.Value.TryGetProperty("operationId", out var opId)
                        ? opId.GetString() ?? $"{methodProp.Name}_{pathProp.Name}"
                        : $"{methodProp.Name}_{pathProp.Name}";

                    var description = methodProp.Value.TryGetProperty("summary", out var sum)
                        ? sum.GetString()
                        : null;

                    operations.Add(new ApiConnectorOperation
                    {
                        Id = _idGen.Generator.NextId(),
                        ConnectorId = connectorId,
                        OperationId = operationId,
                        Method = methodProp.Name.ToUpperInvariant(),
                        Path = pathProp.Name,
                        Description = description
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse OpenAPI spec for connector {Id}", connectorId);
        }

        return operations;
    }
}
