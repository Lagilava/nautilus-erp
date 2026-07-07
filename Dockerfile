# Multi-stage build for the ERP API. Build context is the repo root.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first (cached unless project files change).
COPY ERP.sln ./
COPY src/ ./src/
RUN dotnet restore src/ERP.API/ERP.API.csproj

# Publish a trimmed, release build.
RUN dotnet publish src/ERP.API/ERP.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Bind to 8080 inside the container; the platform maps it externally.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Provide secrets (Jwt__SigningKey, ConnectionStrings__DefaultConnection, etc.) via
# environment variables at deploy time — never bake them into the image.
ENTRYPOINT ["dotnet", "ERP.API.dll"]
