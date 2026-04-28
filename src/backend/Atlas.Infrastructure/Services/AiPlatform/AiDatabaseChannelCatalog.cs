using Atlas.Infrastructure.Channels;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed record AiDatabaseChannelCatalogItem(
    string ChannelKey,
    string DisplayName,
    string? PublishChannelType,
    string? CredentialKind,
    bool AllowDraft = true,
    bool AllowOnline = true);

public static class AiDatabaseChannelCatalog
{
    public static readonly IReadOnlyList<AiDatabaseChannelCatalogItem> All =
        ChannelCatalog.All
            .Select(x => new AiDatabaseChannelCatalogItem(
                x.ChannelKey,
                x.DisplayName,
                x.PublishChannelType,
                x.CredentialKind,
                x.AllowDraft,
                x.AllowOnline))
            .ToList();
}
