using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TripNow.Application.Reservations;

namespace TripNow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ReservationRulesOptions>(configuration.GetSection(ReservationRulesOptions.SectionName));
        services.AddScoped<IReservationService, ReservationService>();
        return services;
    }
}
