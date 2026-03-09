namespace TripNow.Domain.Reservations;

public sealed record ReservationDecision(DateTimeOffset TimestampUtc, string Reason, ReservationStatus ResultingStatus);
