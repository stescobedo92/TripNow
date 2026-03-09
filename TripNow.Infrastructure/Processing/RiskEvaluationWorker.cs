using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TripNow.Application.Abstractions;
using TripNow.Application.Reservations;

namespace TripNow.Infrastructure.Processing;

public sealed class RiskEvaluationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<RiskEvaluationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
                var service = scope.ServiceProvider.GetRequiredService<IReservationService>();

                var pending = await repository.GetPendingRiskCheckAsync(BatchSize, stoppingToken);

                if (pending is { Count: 0 })
                {
                    await Task.Delay(PollingInterval, stoppingToken);
                    continue;
                }

                foreach (var reservation in pending)
                {
                    await service.ProcessRiskEvaluationAsync(reservation.Id, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing pending risk evaluations");
                await Task.Delay(PollingInterval, stoppingToken);
            }
        }
    }
}
