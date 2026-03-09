using TripNow.Domain.Reservations;

namespace TripNow.Application.Reservations;

public sealed record CreateReservationRequest(
    string CustomerEmail,
    string TripCountry,
    decimal Amount,
    string Currency,
    DateTimeOffset TripStartDate);

public sealed record ReservationResponse(
    Guid ReservationId,
    string CustomerEmail,
    string TripCountry,
    decimal Amount,
    string Currency,
    DateTimeOffset TripStartDate,
    ReservationStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RiskEvaluatedAtUtc,
    DateTimeOffset? FinalizedAtUtc,
    RiskDetailsResponse Risk,
    IReadOnlyCollection<ReservationDecisionResponse> Decisions);

public sealed record ReservationDecisionResponse(DateTimeOffset TimestampUtc, string Reason, ReservationStatus ResultingStatus);

public sealed record RiskDetailsResponse(
    RiskEvaluationStatus EvaluationStatus,
    decimal? RiskScore,
    string? ProviderStatus,
    string? ProviderError);
