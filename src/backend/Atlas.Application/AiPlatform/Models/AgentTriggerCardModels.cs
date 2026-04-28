using System.ComponentModel.DataAnnotations;

namespace Atlas.Application.AiPlatform.Models;

public sealed record AgentTriggerDto(
    string Id,
    string AgentId,
    string Name,
    string TriggerType,
    string ConfigJson,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AgentTriggerUpsertRequest(
    [Required, StringLength(128, MinimumLength = 1)] string Name,
    [Required, StringLength(32, MinimumLength = 1)] string TriggerType,
    [Required] string ConfigJson,
    bool IsEnabled);

public sealed record AgentCardDto(
    string Id,
    string AgentId,
    string Name,
    string CardType,
    string SchemaJson,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AgentCardUpsertRequest(
    [Required, StringLength(128, MinimumLength = 1)] string Name,
    [Required, StringLength(32, MinimumLength = 1)] string CardType,
    [Required] string SchemaJson,
    bool IsEnabled);
