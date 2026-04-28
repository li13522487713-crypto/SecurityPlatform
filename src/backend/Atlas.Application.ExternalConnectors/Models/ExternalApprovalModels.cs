using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Application.ExternalConnectors.Models;

public sealed class ExternalApprovalTemplateResponse
{
    public string ExternalTemplateId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public IReadOnlyList<ExternalApprovalTemplateControlDto> Controls { get; set; } = Array.Empty<ExternalApprovalTemplateControlDto>();

    public DateTimeOffset FetchedAt { get; set; }
}

public sealed class ExternalApprovalTemplateControlDto
{
    public string ControlId { get; set; } = string.Empty;

    public string ControlType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public bool Required { get; set; }

    public IReadOnlyList<ExternalApprovalTemplateOptionDto>? Options { get; set; }
}

public sealed class ExternalApprovalTemplateOptionDto
{
    public string Key { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

public sealed class ExternalApprovalTemplateMappingRequest
{
    public long ProviderId { get; set; }

    public long FlowDefinitionId { get; set; }

    public string ExternalTemplateId { get; set; } = string.Empty;

    public IntegrationMode IntegrationMode { get; set; }

    /// <summary>
    /// JSON 数组：[{ "localFieldKey": "...", "externalControlId": "...", "valueType": "string|number|select|file", "transform": "..." }]
    /// </summary>
    public string FieldMappingJson { get; set; } = "[]";

    public bool Enabled { get; set; } = true;
}

public sealed class ExternalApprovalTemplateMappingResponse
{
    public long Id { get; set; }

    public long ProviderId { get; set; }

    public long FlowDefinitionId { get; set; }

    public string ExternalTemplateId { get; set; } = string.Empty;

    public IntegrationMode IntegrationMode { get; set; }

    public string FieldMappingJson { get; set; } = "[]";

    public bool Enabled { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
