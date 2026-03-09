namespace TripNow.Application.Reservations;

public sealed class ReservationValidationException : Exception
{
    public ReservationValidationException(string message)
        : base(message)
    {
    }
}
