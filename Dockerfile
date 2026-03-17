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
