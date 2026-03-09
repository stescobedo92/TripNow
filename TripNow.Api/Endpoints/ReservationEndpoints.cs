using TripNow.Application.Reservations;

namespace TripNow.Api.Endpoints;

public static class ReservationEndpoints
{
    public static RouteGroupBuilder MapReservationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/reservations")
            .WithTags("Reservations");

        group.MapPost(
            "/",
            async (CreateReservationRequest request, IReservationService service, CancellationToken ct) =>
            {
                var result = await service.CreateAsync(request, ct);
                return Results.Accepted($"/reservations/{result.ReservationId}", result);
            })
            .WithName("CreateReservation")
            .WithSummary("Create a new travel reservation")
            .Produces<ReservationResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status500InternalServerError);

        group.MapGet(
            "/{reservationId:guid}",
            async (Guid reservationId, IReservationService service, CancellationToken ct) =>
            {
                var result = await service.GetByIdAsync(reservationId, ct);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .WithName("GetReservationById")
            .WithSummary("Get a reservation by its ID")
            .Produces<ReservationResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet(
            "/",
            async (IReservationService service, CancellationToken ct)
                => Results.Ok(await service.ListAsync(ct)))
            .WithName("ListReservations")
            .WithSummary("List all reservations");

        return group;
    }
}
