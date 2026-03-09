using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TripNow.Application.Abstractions;
using TripNow.Application.Reservations;
using TripNow.Domain.Reservations;
using TripNow.Infrastructure.Persistence;

namespace TripNow.IntegrationTests;

public sealed class ReservationEndpointsTests
{
    [Fact]
    public async Task CreateReservation_And_GetById_Works()
    {
        await using var factory = new TripNowApiFactory();
        var client = factory.CreateClient();

        var request = new CreateReservationRequest(
            "integration@test.com", "MX", 1200, "USD", DateTimeOffset.UtcNow.AddDays(5));

        var createResponse = await client.PostAsJsonAsync("/reservations", request);
        Assert.Equal(HttpStatusCode.Accepted, createResponse.StatusCode);

        var payload = await createResponse.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.NotNull(payload);
        Assert.Equal(ReservationStatus.PendingRiskCheck, payload!.Status);

        var getResponse = await client.GetAsync($"/reservations/{payload.ReservationId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_ReturnsBadRequest_WhenDateIsInvalid()
    {
        await using var factory = new TripNowApiFactory();
        var client = factory.CreateClient();

        var request = new CreateReservationRequest(
            "integration@test.com", "MX", 1200, "USD", DateTimeOffset.UtcNow.AddHours(1));

        var response = await client.PostAsJsonAsync("/reservations", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListReservations_ReturnsOk()
    {
        await using var factory = new TripNowApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/reservations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetReservation_ReturnsNotFound_WhenIdDoesNotExist()
    {
        await using var factory = new TripNowApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/reservations/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_ReturnsRejected_WhenCountryNotSupported()
    {
        await using var factory = new TripNowApiFactory();
        var client = factory.CreateClient();

        var request = new CreateReservationRequest(
            "test@test.com", "ZZ", 500, "USD", DateTimeOffset.UtcNow.AddDays(5));

        var response = await client.PostAsJsonAsync("/reservations", request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.NotNull(payload);
        Assert.Equal(ReservationStatus.Rejected, payload!.Status);
    }

    private sealed class TripNowApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = $"TripNow_Test_{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<TripNowDbContext>));
                if (descriptor is not null) services.Remove(descriptor);

                services.AddDbContext<TripNowDbContext>(options =>
                    options.UseInMemoryDatabase(_dbName));

                services.RemoveAll<IRiskProviderClient>();
                services.AddSingleton<IRiskProviderClient>(new AlwaysApprovedRiskClient());
            });
        }
    }

    private sealed class AlwaysApprovedRiskClient : IRiskProviderClient
    {
        public Task<RiskProviderResult> EvaluateAsync(RiskProviderRequest request, CancellationToken ct)
            => Task.FromResult(new RiskProviderResult(true, "APPROVED", 12, null));
    }
}
