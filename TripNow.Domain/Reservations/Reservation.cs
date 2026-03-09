namespace TripNow.Domain.Reservations;

public sealed class Reservation
{
    private readonly List<ReservationDecision> _decisions = [];

    private Reservation()
    {
    }

    public Guid Id { get; private set; }
    public string CustomerEmail { get; private set; } = string.Empty;
    public string TripCountry { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateTimeOffset TripStartDate { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RiskEvaluatedAtUtc { get; private set; }
    public DateTimeOffset? FinalizedAtUtc { get; private set; }
    public ReservationStatus Status { get; private set; }
    public bool RequiresPositiveRiskEvaluation { get; private set; }
    public RiskEvaluationStatus RiskEvaluationStatus { get; private set; }
    public decimal? RiskScore { get; private set; }
    public string? RiskProviderRawStatus { get; private set; }
    public string? RiskProviderError { get; private set; }
    public IReadOnlyCollection<ReservationDecision> Decisions => _decisions;

    public static Reservation CreatePending(
        Guid id,
        string customerEmail,
        string tripCountry,
        decimal amount,
        string currency,
        DateTimeOffset tripStartDate,
        DateTimeOffset createdAtUtc,
        bool requiresPositiveRiskEvaluation)
    {
        var reservation = new Reservation
        {
            Id = id,
            CustomerEmail = customerEmail,
            TripCountry = tripCountry,
            Amount = amount,
            Currency = currency,
            TripStartDate = tripStartDate,
            CreatedAtUtc = createdAtUtc,
            Status = ReservationStatus.PendingRiskCheck,
            RequiresPositiveRiskEvaluation = requiresPositiveRiskEvaluation,
            RiskEvaluationStatus = RiskEvaluationStatus.Unknown
        };

        reservation.AddDecision("Reservation created and queued for risk evaluation.", ReservationStatus.PendingRiskCheck, createdAtUtc);
        return reservation;
    }

    public static Reservation CreateRejected(
        Guid id,
        string customerEmail,
        string tripCountry,
        decimal amount,
        string currency,
        DateTimeOffset tripStartDate,
        DateTimeOffset createdAtUtc,
        string reason)
    {
        var reservation = new Reservation
        {
            Id = id,
            CustomerEmail = customerEmail,
            TripCountry = tripCountry,
            Amount = amount,
            Currency = currency,
            TripStartDate = tripStartDate,
            CreatedAtUtc = createdAtUtc,
            Status = ReservationStatus.Rejected,
            FinalizedAtUtc = createdAtUtc,
            RiskEvaluationStatus = RiskEvaluationStatus.Unknown
        };

        reservation.AddDecision(reason, ReservationStatus.Rejected, createdAtUtc);
        return reservation;
    }

    public void MarkRiskApproved(decimal riskScore, string providerStatus, DateTimeOffset atUtc)
    {
        RiskEvaluatedAtUtc = atUtc;
        RiskEvaluationStatus = RiskEvaluationStatus.Approved;
        RiskScore = riskScore;
        RiskProviderRawStatus = providerStatus;
        RiskProviderError = null;
        Status = ReservationStatus.Approved;
        FinalizedAtUtc = atUtc;
        AddDecision("Risk provider approved reservation.", ReservationStatus.Approved, atUtc);
    }

    public void MarkRiskRejected(decimal riskScore, string providerStatus, DateTimeOffset atUtc)
    {
        RiskEvaluatedAtUtc = atUtc;
        RiskEvaluationStatus = RiskEvaluationStatus.Rejected;
        RiskScore = riskScore;
        RiskProviderRawStatus = providerStatus;
        RiskProviderError = null;
        Status = ReservationStatus.Rejected;
        FinalizedAtUtc = atUtc;
        AddDecision("Risk provider rejected reservation.", ReservationStatus.Rejected, atUtc);
    }

    public void MarkRiskError(string error, DateTimeOffset atUtc)
    {
        RiskEvaluatedAtUtc = atUtc;
        RiskEvaluationStatus = RiskEvaluationStatus.Error;
        RiskProviderError = error;

        if (RequiresPositiveRiskEvaluation)
        {
            Status = ReservationStatus.Rejected;
            FinalizedAtUtc = atUtc;
            AddDecision("Risk provider failed and reservation required positive risk evaluation.", ReservationStatus.Rejected, atUtc);
            return;
        }

        Status = ReservationStatus.Approved;
        FinalizedAtUtc = atUtc;
        AddDecision("Risk provider failed. Reservation approved by fallback policy for non-high-exposure case.", ReservationStatus.Approved, atUtc);
    }

    private void AddDecision(string reason, ReservationStatus resultingStatus, DateTimeOffset atUtc)
    {
        _decisions.Add(new ReservationDecision(atUtc, reason, resultingStatus));
    }
}
