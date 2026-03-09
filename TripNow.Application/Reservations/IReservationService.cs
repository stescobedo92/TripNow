namespace TripNow.Application.Reservations;

public interface IReservationService
{
    Task<ReservationResponse> CreateAsync(CreateReservationRequest request, CancellationToken ct);
    Task<ReservationResponse?> GetByIdAsync(Guid reservationId, CancellationToken ct);
    Task<IReadOnlyList<ReservationResponse>> ListAsync(CancellationToken ct);
    Task ProcessRiskEvaluationAsync(Guid reservationId, CancellationToken ct);
}
