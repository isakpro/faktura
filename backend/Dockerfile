# Faktura API — container för Render (Docker).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore (cachar lager på projektfiler)
COPY Faktura.slnx ./
COPY src/Faktura.Api/Faktura.Api.csproj src/Faktura.Api/
COPY src/Faktura.Domain/Faktura.Domain.csproj src/Faktura.Domain/
COPY src/Faktura.Infrastructure/Faktura.Infrastructure.csproj src/Faktura.Infrastructure/
RUN dotnet restore src/Faktura.Api/Faktura.Api.csproj

# Build + publish
COPY src/ src/
RUN dotnet publish src/Faktura.Api/Faktura.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# curl behövs för container-healthchecken (compose/orkestrering).
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
# Render sätter PORT; lyssna på den (fallback 8080 lokalt).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet Faktura.Api.dll"]
