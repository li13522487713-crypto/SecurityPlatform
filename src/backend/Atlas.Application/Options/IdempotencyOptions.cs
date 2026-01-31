namespace Atlas.Application.Options;

public sealed class IdempotencyOptions
{
    public string HeaderName { get; set; } = "Idempotency-Key";
    public int RetentionHours { get; set; } = 24;
    public int CleanupIntervalMinutes { get; set; } = 60;
    public int MaxKeyLength { get; set; } = 128;
}
