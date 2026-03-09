using TripNow.Domain.Reservations;

namespace TripNow.Application.Abstractions;

public interface IReservationRepository
{
    Task AddAsync(Reservation reservation, CancellationToken ct);
    Task<Reservation?> GetByIdAsync(Guid reservationId, CancellationToken ct);
    Task<IReadOnlyList<Reservation>> ListAsync(CancellationToken ct);
    Task<int> CountPendingForCustomerSinceAsync(string customerEmail, DateTimeOffset sinceUtc, CancellationToken ct);
    Task<IReadOnlyList<Reservation>> GetPendingRiskCheckAsync(int batchSize, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
