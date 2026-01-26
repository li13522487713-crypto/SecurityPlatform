namespace Atlas.Application.Alert.Models;

public sealed record AlertListItem(Guid Id, string Title, DateTimeOffset CreatedAt);