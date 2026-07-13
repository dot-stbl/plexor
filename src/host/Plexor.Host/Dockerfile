# Plexor.Host Dockerfile — multi-stage .NET 10 build.
# Stage 1: build the host executable.
# Stage 2: copy into ASP.NET runtime image.
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

COPY . .

RUN dotnet restore src/host/Plexor.Host/Plexor.Host.csproj
RUN dotnet publish src/host/Plexor.Host/Plexor.Host.csproj \
        -c Release \
        -o /app/publish \
        --no-restore \
        /p:UseAppHost=true

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ConnectionStrings__Postgres="Host=postgres;Database=plexor;Username=plexor;Password=plexor"
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS="http://+:8080"

EXPOSE 8080

ENTRYPOINT ["dotnet", "Plexor.Host.dll"]
