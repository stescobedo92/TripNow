FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY TripNow.Domain/TripNow.Domain.csproj TripNow.Domain/
COPY TripNow.Application/TripNow.Application.csproj TripNow.Application/
COPY TripNow.Infrastructure/TripNow.Infrastructure.csproj TripNow.Infrastructure/
COPY TripNow.Api/TripNow.Api.csproj TripNow.Api/
COPY TripNow.slnx .
RUN dotnet restore TripNow.Api/TripNow.Api.csproj

COPY . .
RUN dotnet publish TripNow.Api/TripNow.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --no-create-home appuser
USER appuser

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "TripNow.Api.dll"]
