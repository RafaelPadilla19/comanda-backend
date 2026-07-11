# Comanda.Api — imagen para Cloud Run (o cualquier host de contenedores).
# Build:  docker build -t comanda-api .
# Sin secretos horneados: TODO (connection string, JWT, llaves) llega por env vars.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore cacheable: primero solo los .csproj.
COPY src/Comanda.Domain/Comanda.Domain.csproj src/Comanda.Domain/
COPY src/Comanda.Infrastructure/Comanda.Infrastructure.csproj src/Comanda.Infrastructure/
COPY src/Comanda.Api/Comanda.Api.csproj src/Comanda.Api/
RUN dotnet restore src/Comanda.Api/Comanda.Api.csproj

COPY src/ src/
RUN dotnet publish src/Comanda.Api/Comanda.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
# Las imágenes aspnet 8+ escuchan en 8080 (puerto por defecto de Cloud Run).
EXPOSE 8080
ENTRYPOINT ["dotnet", "Comanda.Api.dll"]
