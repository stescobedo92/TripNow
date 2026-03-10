using Microsoft.EntityFrameworkCore;
using TripNow.Application.Abstractions;
using TripNow.Domain.Reservations;

namespace TripNow.Infrastructure.Persistence;

public sealed class ReservationRepository(TripNowDbContext dbContext) : IReservationRepository
{
    public Task AddAsync(Reservation reservation, CancellationToken ct)
    {
        dbContext.Reservations.Add(reservation);
        return Task.CompletedTask;
    }

    public async Task<Reservation?> GetByIdAsync(Guid reservationId, CancellationToken ct)
        => await dbContext.Reservations
            .Include(r => r.Decisions)
            .FirstOrDefaultAsync(r => r.Id == reservationId, ct);

    public async Task<IReadOnlyList<Reservation>> ListAsync(CancellationToken ct)
        => await dbContext.Reservations
            .Include(r => r.Decisions)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<int> CountPendingForCustomerSinceAsync(
        string customerEmail, DateTimeOffset sinceUtc, CancellationToken ct)
        => await dbContext.Reservations
            .CountAsync(r =>
                r.CustomerEmail == customerEmail
                && r.Status == ReservationStatus.PendingRiskCheck
                && r.CreatedAtUtc >= sinceUtc, ct);

    public async Task<IReadOnlyList<Reservation>> GetPendingRiskCheckAsync(int batchSize, CancellationToken ct)
        => await dbContext.Reservations
            .Where(r => r.Status == ReservationStatus.PendingRiskCheck)
            .OrderBy(r => r.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct)
        => dbContext.SaveChangesAsync(ct);
}
