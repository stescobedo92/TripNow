using TripNow.Api.Endpoints;
using TripNow.Api.Middleware;
using TripNow.Application;
using TripNow.Infrastructure;
using TripNow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TripNowDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseGlobalExceptionHandler();
app.MapHealthChecks("/health");
app.MapReservationEndpoints();

app.Run();

public partial class Program;
