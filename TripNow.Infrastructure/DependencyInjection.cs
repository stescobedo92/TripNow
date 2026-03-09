using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TripNow.Application.Abstractions;
using TripNow.Infrastructure.Persistence;
using TripNow.Infrastructure.Processing;
using TripNow.Infrastructure.Risk;

namespace TripNow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RiskProviderOptions>(configuration.GetSection(RiskProviderOptions.SectionName));

        var connectionString = configuration.GetConnectionString("TripNowDb")
            ?? throw new InvalidOperationException("Connection string 'TripNowDb' is required.");

        services.AddDbContext<TripNowDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddSingleton(TimeProvider.System);
        services.AddHostedService<RiskEvaluationWorker>();

        services.AddHttpClient<IRiskProviderClient, HttpRiskProviderClient>((sp, client) =>
        {
            var riskOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RiskProviderOptions>>().Value;
            client.BaseAddress = new Uri(riskOptions.BaseUrl);
        });

        return services;
    }
}
