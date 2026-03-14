using System.ComponentModel.DataAnnotations;
using Atlas.Core.Models;
using Atlas.Domain.System.Entities;

namespace Atlas.Application.System.Models;

public sealed record TenantDto(
    long Id,
    string Name,
    string Code,
    string? Description,
    long? AdminUserId,
    bool IsActive,
    TenantStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record TenantCreateRequest(
    string Name,
    string Code,
    string? Description,
    long? AdminUserId);

public sealed record TenantUpdateRequest(
    long Id,
    string Name,
    string Code,
    string? Description,
    long? AdminUserId);

public record TenantQueryRequest
{
    public int PageIndex { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Keyword { get; init; }
    
    /// <summary>
    /// 是否激活（可选）
    /// </summary>
    public bool? IsActive { get; init; }
}
