namespace Atlas.Core.Identity;

public sealed record ClientContextView(
    string ClientType,
    string ClientPlatform,
    string ClientChannel,
    string ClientAgent);
