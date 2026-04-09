namespace Atlas.Infrastructure.Options;

public sealed class AtlasRabbitMqTransportOptions
{
    public const string SectionName = "Messaging:RabbitMq";

    public bool Enabled { get; init; }

    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 5672;

    public string VirtualHost { get; init; } = "/";

    public string Username { get; init; } = "guest";

    public string Password { get; init; } = "guest";
}
