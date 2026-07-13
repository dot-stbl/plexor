# Plexor.Migrator Dockerfile — multi-stage .NET 10 build.
# Stage 1: build the Migrator executable.
# Stage 2: copy into ASP.NET runtime image.
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Copy the entire solution so project references resolve. Adjust if the
# repo is mounted under a different path inside the container.
COPY . .

RUN dotnet restore src/host/Plexor.Migrator/Plexor.Migrator.csproj
RUN dotnet publish src/host/Plexor.Migrator/Plexor.Migrator.csproj \
        -c Release \
        -o /app/publish \
        --no-restore \
        /p:UseAppHost=true

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Override at `docker run` time with the real connection string.
ENV ConnectionStrings__Postgres="Host=postgres;Database=plexor;Username=plexor;Password=plexor"
ENV DOTNET_ENVIRONMENT=Production

# Default subcommand is `migrate`; override with `docker run plexor-migrator seed <name>`
# or any other subcommand Plexor.Migrator supports.
ENTRYPOINT ["dotnet", "plexor-migrator.dll"]
CMD ["migrate"]
