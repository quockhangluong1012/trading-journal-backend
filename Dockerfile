# syntax=docker/dockerfile:1

# Multi-stage build for the TradingJournal API Gateway (the single entry point that
# references every module, so publishing it builds the whole application).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the full source. A modular monolith with ~20 interdependent projects restores most
# reliably from a full copy; Directory.Build.props/.editorconfig are picked up automatically.
COPY . .

RUN dotnet restore bootstrapper/TradingJournal.ApiGateway/TradingJournal.ApiGateway.csproj
RUN dotnet publish bootstrapper/TradingJournal.ApiGateway/TradingJournal.ApiGateway.csproj \
    -c Release -o /app/publish --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Kestrel listens on 8080 inside the container (non-root friendly).
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TradingJournal.ApiGateway.dll"]
