namespace TripNow.Infrastructure.Risk;

public sealed class RiskProviderOptions
{
    public const string SectionName = "RiskProvider";
    public string BaseUrl { get; init; } = string.Empty;
    public int TimeoutMs { get; init; } = 1200;
    public int MaxRetries { get; init; } = 2;
}
