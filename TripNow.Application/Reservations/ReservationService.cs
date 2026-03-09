using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TripNow.Application.Abstractions;
using TripNow.Domain.Reservations;

namespace TripNow.Application.Reservations;

public sealed class ReservationService(
    IReservationRepository repository,
    IRiskProviderClient riskProviderClient,
    TimeProvider timeProvider,
    IOptions<ReservationRulesOptions> options,
    ILogger<ReservationService> logger) : IReservationService
{
    private readonly ReservationRulesOptions _rules = options.Value;

    public async Task<ReservationResponse> CreateAsync(CreateReservationRequest request, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        Validate(request, now);

        var pendingCount = await repository.CountPendingForCustomerSinceAsync(
            request.CustomerEmail, now.AddHours(-24), ct);

        if (pendingCount >= _rules.MaxPendingReservationsIn24h)
            return await RejectAndSaveAsync(request, now, "Customer exceeded pending reservations limit in 24 hours.", ct);

        if (!IsCountrySupported(request.TripCountry))
            return await RejectAndSaveAsync(request, now, "Trip country is not supported.", ct);

        var requiresPositiveRisk = request.Amount >= _rules.HighExposureAmount
                                   || IsHighRiskCountry(request.TripCountry);

        var pending = Reservation.CreatePending(
            Guid.NewGuid(), request.CustomerEmail, request.TripCountry,
            request.Amount, request.Currency, request.TripStartDate,
            now, requiresPositiveRisk);

        await repository.AddAsync(pending, ct);
        await repository.SaveChangesAsync(ct);

        return ToResponse(pending);
    }

    public async Task<ReservationResponse?> GetByIdAsync(Guid reservationId, CancellationToken ct)
    {
        var reservation = await repository.GetByIdAsync(reservationId, ct);
        return reservation is not null ? ToResponse(reservation) : null;
    }

    public async Task<IReadOnlyList<ReservationResponse>> ListAsync(CancellationToken ct)
    {
        var reservations = await repository.ListAsync(ct);
        return [.. reservations.Select(ToResponse)];
    }

    public async Task ProcessRiskEvaluationAsync(Guid reservationId, CancellationToken ct)
    {
        var reservation = await repository.GetByIdAsync(reservationId, ct);

        if (reservation is not { Status: ReservationStatus.PendingRiskCheck })
            return;

        var now = timeProvider.GetUtcNow();

        try
        {
            var result = await riskProviderClient.EvaluateAsync(
                new RiskProviderRequest(reservation.CustomerEmail, reservation.TripCountry, reservation.Amount), ct);

            switch (result)
            {
                case { Success: false }:
                    reservation.MarkRiskError(result.Error ?? "Risk provider failure.", now);
                    break;
                case { ProviderStatus: var s } when s.Equals("APPROVED", StringComparison.OrdinalIgnoreCase):
                    reservation.MarkRiskApproved(result.RiskScore, result.ProviderStatus, now);
                    break;
                default:
                    reservation.MarkRiskRejected(result.RiskScore, result.ProviderStatus, now);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Risk evaluation failed for reservation {ReservationId}", reservationId);
            reservation.MarkRiskError(ex.Message, now);
        }

        await repository.SaveChangesAsync(ct);
    }

    private async Task<ReservationResponse> RejectAndSaveAsync(
        CreateReservationRequest request, DateTimeOffset now, string reason, CancellationToken ct)
    {
        var rejected = Reservation.CreateRejected(
            Guid.NewGuid(), request.CustomerEmail, request.TripCountry,
            request.Amount, request.Currency, request.TripStartDate,
            now, reason);

        await repository.AddAsync(rejected, ct);
        await repository.SaveChangesAsync(ct);
        return ToResponse(rejected);
    }

    private void Validate(CreateReservationRequest request, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerEmail))
            throw new ReservationValidationException("customerEmail is required.");

        if (string.IsNullOrWhiteSpace(request.TripCountry))
            throw new ReservationValidationException("tripCountry is required.");

        if (string.IsNullOrWhiteSpace(request.Currency))
            throw new ReservationValidationException("currency is required.");

        if (_rules.SupportedCurrencies is { Count: > 0 } currencies
            && !currencies.Exists(c => c.Equals(request.Currency, StringComparison.OrdinalIgnoreCase)))
            throw new ReservationValidationException("currency is not supported.");

        if (request.Amount <= 0 || request.Amount > _rules.MaxAmount)
            throw new ReservationValidationException($"amount must be between 0 and {_rules.MaxAmount}.");

        switch (request.TripStartDate)
        {
            case var d when d < now:
                throw new ReservationValidationException("tripStartDate cannot be in the past.");
            case var d when d < now.AddHours(_rules.MinHoursBeforeTrip):
                throw new ReservationValidationException($"tripStartDate must be at least {_rules.MinHoursBeforeTrip} hours ahead.");
        }
    }

    private bool IsCountrySupported(string country)
        => _rules.SupportedCountries.Exists(x => x.Equals(country, StringComparison.OrdinalIgnoreCase));

    private bool IsHighRiskCountry(string country)
        => _rules.HighRiskCountries.Exists(x => x.Equals(country, StringComparison.OrdinalIgnoreCase));

    private static ReservationResponse ToResponse(Reservation r) => new(
        r.Id, r.CustomerEmail, r.TripCountry, r.Amount, r.Currency, r.TripStartDate,
        r.Status, r.CreatedAtUtc, r.RiskEvaluatedAtUtc, r.FinalizedAtUtc,
        new RiskDetailsResponse(r.RiskEvaluationStatus, r.RiskScore, r.RiskProviderRawStatus, r.RiskProviderError),
        [.. r.Decisions.Select(d => new ReservationDecisionResponse(d.TimestampUtc, d.Reason, d.ResultingStatus))]);
}
