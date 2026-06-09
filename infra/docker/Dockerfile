# ── Build stage ────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore layer — cached unless csproj files change
COPY src/WizAccountant.Contracts/WizAccountant.Contracts.csproj WizAccountant.Contracts/
COPY src/WizAccountant.Api/WizAccountant.Api.csproj              WizAccountant.Api/
RUN dotnet restore WizAccountant.Api/WizAccountant.Api.csproj

# Copy source and publish
COPY src/WizAccountant.Contracts/ WizAccountant.Contracts/
COPY src/WizAccountant.Api/       WizAccountant.Api/
RUN dotnet publish WizAccountant.Api/WizAccountant.Api.csproj \
    -c Release --no-restore -o /app/publish

# ── Runtime stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Non-root user for security
RUN adduser --disabled-password --gecos "" appuser \
    && chown appuser /app \
    && mkdir -p /data && chown appuser /data
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__DefaultConnection="Data Source=/data/wizaccountant.db"

VOLUME ["/data"]
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD bash -c 'exec 3<>/dev/tcp/localhost/8080 && echo -e "GET /health HTTP/1.0\r\nHost: localhost\r\n\r" >&3 && head -1 <&3 | grep -q "200 OK"'

ENTRYPOINT ["dotnet", "WizAccountant.Api.dll"]
