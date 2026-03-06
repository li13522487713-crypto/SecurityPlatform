using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Integration;

/// <summary>
/// OpenAPI 连接器（外部 REST API 集成配置）
/// </summary>
public sealed class ApiConnector : TenantEntity
{
    public ApiConnector()
        : base(TenantId.Empty)
    {
    }

    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public ApiAuthType AuthType { get; set; }
    public string? AuthConfig { get; set; }
    public string? OpenApiSpecUrl { get; set; }
    public string? HealthCheckUrl { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// OpenAPI 连接器操作（从 spec 同步的操作列表）
/// </summary>
public sealed class ApiConnectorOperation
{
    public long Id { get; set; }
    public long ConnectorId { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RequestSchema { get; set; }
    public string? ResponseSchema { get; set; }
}

public enum ApiAuthType
{
    None = 0,
    ApiKey = 1,
    Bearer = 2,
    BasicAuth = 3,
    OAuth2 = 4
}
