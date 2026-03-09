using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TripNow.Application.Abstractions;

namespace TripNow.Infrastructure.Risk;

public sealed class HttpRiskProviderClient(
    HttpClient httpClient,
    IOptions<RiskProviderOptions> options,
    ILogger<HttpRiskProviderClient> logger) : IRiskProviderClient
{
    private readonly RiskProviderOptions _options = options.Value;

    public async Task<RiskProviderResult> EvaluateAsync(RiskProviderRequest request, CancellationToken ct)
    {
        var payload = new { request.CustomerEmail, request.TripCountry, request.Amount };

        for (var attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(_options.TimeoutMs));

                var response = await httpClient.PostAsJsonAsync("/risk-evaluation", payload, timeoutCts.Token);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<RiskProviderResponse>(cancellationToken: timeoutCts.Token);

                return result switch
                {
                    null => new(false, "UNKNOWN", 0, "Empty risk response."),
                    _ => new(true, result.Status ?? "UNKNOWN", result.RiskScore, null)
                };
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && attempt < _options.MaxRetries)
            {
                logger.LogWarning("Risk provider timeout on attempt {Attempt}/{MaxRetries}", attempt + 1, _options.MaxRetries);
            }
            catch (Exception ex) when (attempt < _options.MaxRetries)
            {
                logger.LogWarning(ex, "Risk provider failure on attempt {Attempt}/{MaxRetries}", attempt + 1, _options.MaxRetries);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * (1 << attempt)), ct);
            }
            catch (Exception ex)
            {
                return new(false, "ERROR", 0, ex.Message);
            }
        }

        return new(false, "ERROR", 0, "Risk provider retries exhausted.");
    }

    private sealed record RiskProviderResponse(decimal RiskScore, string? Status);
}
