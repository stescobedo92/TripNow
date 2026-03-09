namespace TripNow.Application.Reservations;

public sealed class ReservationRulesOptions
{
    public const string SectionName = "ReservationRules";
    public decimal MaxAmount { get; init; } = 20000m;
    public decimal HighExposureAmount { get; init; } = 5000m;
    public int MinHoursBeforeTrip { get; init; } = 48;
    public int MaxPendingReservationsIn24h { get; init; } = 3;
    public List<string> SupportedCountries { get; init; } = [];
    public List<string> HighRiskCountries { get; init; } = [];
    public List<string> SupportedCurrencies { get; init; } = [];
}
