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

        var connectionString = BuildConnectionString(configuration);

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

    private static string BuildConnectionString(IConfiguration configuration)
    {
        // Railway (and other cloud providers) set DATABASE_URL as postgresql://user:pass@host:port/db
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrEmpty(databaseUrl))
        {
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':');
            return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
        }

        return configuration.GetConnectionString("TripNowDb")
            ?? throw new InvalidOperationException("Connection string 'TripNowDb' is required. Set DATABASE_URL or ConnectionStrings__TripNowDb.");
    }
}
