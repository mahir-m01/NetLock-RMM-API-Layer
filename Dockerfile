# ─── Build stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Restore dependencies first (layer-cached)
COPY src/ControlIT.Api/ControlIT.Api.csproj src/ControlIT.Api/
RUN dotnet restore src/ControlIT.Api/ControlIT.Api.csproj

# Copy source and publish
COPY src/ControlIT.Api/ src/ControlIT.Api/
RUN dotnet publish src/ControlIT.Api/ControlIT.Api.csproj \
      -c Release \
      -o /app/publish \
      --no-restore

# ─── Runtime stage ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 5290
ENV ASPNETCORE_URLS=http://+:5290
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ControlIT.Api.dll"]
