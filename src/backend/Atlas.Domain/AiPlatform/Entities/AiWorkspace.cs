using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiWorkspace : TenantEntity
{
    public AiWorkspace()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Theme = "light";
        LastVisitedPath = "/ai/workspace";
        FavoriteResourceIdsJson = "[]";
        CreatedAt = DateTime.UtcNow;
    }

    public AiWorkspace(
        TenantId tenantId,
        long userId,
        string name,
        string? theme,
        string? lastVisitedPath,
        string? favoriteResourceIdsJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        UserId = userId;
        Name = name;
        Theme = string.IsNullOrWhiteSpace(theme) ? "light" : theme;
        LastVisitedPath = string.IsNullOrWhiteSpace(lastVisitedPath) ? "/ai/workspace" : lastVisitedPath;
        FavoriteResourceIdsJson = string.IsNullOrWhiteSpace(favoriteResourceIdsJson) ? "[]" : favoriteResourceIdsJson;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long UserId { get; private set; }
    public string Name { get; private set; }
    public string Theme { get; private set; }
    public string LastVisitedPath { get; private set; }
    public string FavoriteResourceIdsJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Update(
        string name,
        string theme,
        string lastVisitedPath,
        string favoriteResourceIdsJson)
    {
        Name = name;
        Theme = theme;
        LastVisitedPath = lastVisitedPath;
        FavoriteResourceIdsJson = favoriteResourceIdsJson;
        UpdatedAt = DateTime.UtcNow;
    }
}
