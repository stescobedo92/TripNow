using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TripNow.Application.Abstractions;
using TripNow.Application.Reservations;
using TripNow.Domain.Reservations;

namespace TripNow.UnitTests;

public sealed class ReservationServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 3, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_Throws_WhenAmountIsInvalid()
    {
        var service = BuildService();
        var request = new CreateReservationRequest("a@b.com", "MX", 0, "USD", FixedNow.AddDays(5));

        await Assert.ThrowsAsync<ReservationValidationException>(
            () => service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_ReturnsRejected_WhenCountryNotSupported()
    {
        var repository = new InMemoryReservationRepository();
        var service = BuildService(repository: repository);

        var result = await service.CreateAsync(
            new("a@b.com", "ZZ", 300, "USD", FixedNow.AddDays(5)),
            CancellationToken.None);

        Assert.Equal(ReservationStatus.Rejected, result.Status);
        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task ProcessRiskEvaluation_Rejected_WhenProviderFails_AndHighExposure()
    {
        var repository = new InMemoryReservationRepository();
        var riskClient = new FakeRiskClient(new RiskProviderResult(false, "ERROR", 0, "timeout"));
        var service = BuildService(repository, riskClient);

        var created = await service.CreateAsync(
            new("x@y.com", "MX", 7000, "USD", FixedNow.AddDays(6)),
            CancellationToken.None);

        await service.ProcessRiskEvaluationAsync(created.ReservationId, CancellationToken.None);
        var stored = await repository.GetByIdAsync(created.ReservationId, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(ReservationStatus.Rejected, stored!.Status);
        Assert.Equal(RiskEvaluationStatus.Error, stored.RiskEvaluationStatus);
    }

    [Fact]
    public async Task ProcessRiskEvaluation_Approved_WhenProviderFails_AndLowExposure()
    {
        var repository = new InMemoryReservationRepository();
        var riskClient = new FakeRiskClient(new RiskProviderResult(false, "ERROR", 0, "timeout"));
        var service = BuildService(repository, riskClient);

        var created = await service.CreateAsync(
            new("x@y.com", "MX", 500, "USD", FixedNow.AddDays(6)),
            CancellationToken.None);

        await service.ProcessRiskEvaluationAsync(created.ReservationId, CancellationToken.None);
        var stored = await repository.GetByIdAsync(created.ReservationId, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(ReservationStatus.Approved, stored!.Status);
    }

    [Fact]
    public async Task CreateAsync_RejectsWhenPendingLimitExceeded()
    {
        var repository = new InMemoryReservationRepository();
        var service = BuildService(repository: repository);

        for (var i = 0; i < 3; i++)
        {
            await service.CreateAsync(
                new("limit@test.com", "MX", 100, "USD", FixedNow.AddDays(5 + i)),
                CancellationToken.None);
        }

        var fourth = await service.CreateAsync(
            new("limit@test.com", "MX", 100, "USD", FixedNow.AddDays(10)),
            CancellationToken.None);

        Assert.Equal(ReservationStatus.Rejected, fourth.Status);
        Assert.Contains("exceeded pending reservations limit", fourth.Decisions.First().Reason);
    }

    private static ReservationService BuildService(
        InMemoryReservationRepository? repository = null,
        FakeRiskClient? riskClient = null)
    {
        var rules = Options.Create(new ReservationRulesOptions
        {
            SupportedCountries = ["MX", "US"],
            HighRiskCountries = ["NG"]
        });

        return new ReservationService(
            repository ?? new InMemoryReservationRepository(),
            riskClient ?? new FakeRiskClient(new RiskProviderResult(true, "APPROVED", 10, null)),
            new FixedTimeProvider(FixedNow),
            rules,
            NullLogger<ReservationService>.Instance);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeRiskClient(RiskProviderResult result) : IRiskProviderClient
    {
        public Task<RiskProviderResult> EvaluateAsync(RiskProviderRequest request, CancellationToken ct)
            => Task.FromResult(result);
    }

    private sealed class InMemoryReservationRepository : IReservationRepository
    {
        public List<Reservation> Items { get; } = [];

        public Task AddAsync(Reservation reservation, CancellationToken ct)
        {
            Items.Add(reservation);
            return Task.CompletedTask;
        }

        public Task<Reservation?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<Reservation>> ListAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Reservation>>(Items);

        public Task<int> CountPendingForCustomerSinceAsync(string email, DateTimeOffset since, CancellationToken ct)
            => Task.FromResult(Items.Count(x =>
                x.CustomerEmail == email
                && x.Status == ReservationStatus.PendingRiskCheck
                && x.CreatedAtUtc >= since));

        public Task<IReadOnlyList<Reservation>> GetPendingRiskCheckAsync(int batchSize, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Reservation>>(
                Items.Where(x => x.Status == ReservationStatus.PendingRiskCheck)
                     .Take(batchSize).ToList());

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
