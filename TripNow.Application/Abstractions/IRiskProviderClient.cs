namespace TripNow.Application.Abstractions;

public interface IRiskProviderClient
{
    Task<RiskProviderResult> EvaluateAsync(RiskProviderRequest request, CancellationToken ct);
}

public sealed record RiskProviderRequest(string CustomerEmail, string TripCountry, decimal Amount);

public sealed record RiskProviderResult(bool Success, string ProviderStatus, decimal RiskScore, string? Error);
