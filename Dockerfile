# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files first for layer caching
COPY Shared/FourPlayWebApp.Shared.csproj Shared/
COPY Server/FourPlayWebApp.Server.csproj Server/
RUN dotnet restore Server/FourPlayWebApp.Server.csproj

# Copy source and publish
COPY Shared/ Shared/
COPY Server/ Server/
# DEMO_MODE fixtures embedded by Server.csproj (EmbeddedResource Include="..\sample_espn_*.json")
# live at the repo root, one level above Server/ -- must be copied into the build context here too.
COPY sample_espn_nfl.json sample_espn_nfl_final.json sample_espn_nfl_halftime.json \
    sample_espn_nfl_scheduled.json sample_espn_nfl_in_progress.json sample_espn_cfb.json ./
RUN dotnet publish Server/FourPlayWebApp.Server.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Non-root user for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FourPlayWebApp.Server.dll"]
