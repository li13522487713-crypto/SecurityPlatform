namespace Atlas.Application.LowCode.Models;

public sealed record AppTemplateDto(
    string Id,
    string Code,
    string Name,
    string Kind,
    string? Description,
    string? IndustryTag,
    string TemplateJson,
    string ShareScope,
    int Stars,
    int UseCount,
    string CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AppTemplateUpsertRequest(
    string? Id,
    string Code,
    string Name,
    string Kind,
    string? Description,
    string? IndustryTag,
    string TemplateJson,
    string? ShareScope);

public sealed record AppTemplateApplyRequest(string TemplateId);
public sealed record AppTemplateApplyResult(string TemplateId, string TemplateJson, int UseCount);
