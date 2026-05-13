# syntax=docker/dockerfile:1.7

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Restore as a distinct layer for better caching
COPY src/AiGateway.Api/AiGateway.Api.csproj src/AiGateway.Api/
RUN dotnet restore src/AiGateway.Api/AiGateway.Api.csproj

# Copy the rest and publish
COPY src/ src/
RUN dotnet publish src/AiGateway.Api/AiGateway.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Cloud Run injects PORT (default 8080). ASPNETCORE_URLS makes Kestrel
# bind to it without code changes; PORT is honored explicitly in Program.cs.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0

# Non-root user (image already provides UID 1654 as 'app')
USER app
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "AiGateway.Api.dll"]
