namespace Atlas.Application.AiPlatform.Models;

public sealed record AiShortcutCommandItem(
    long Id,
    string CommandKey,
    string DisplayName,
    string TargetPath,
    string? Description,
    int SortOrder,
    bool IsEnabled);

public sealed record AiShortcutCommandCreateRequest(
    string CommandKey,
    string DisplayName,
    string TargetPath,
    string? Description,
    int SortOrder);

public sealed record AiShortcutCommandUpdateRequest(
    string DisplayName,
    string TargetPath,
    string? Description,
    int SortOrder,
    bool IsEnabled);

public sealed record AiBotPopupInfoDto(
    long Id,
    string PopupCode,
    string Title,
    string Content,
    bool Dismissed,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiBotPopupDismissRequest(string PopupCode, bool Dismissed);
