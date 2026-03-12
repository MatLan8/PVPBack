# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY PVPBack.sln ./
COPY PVPBack.Api/PVPBack.Api.csproj PVPBack.Api/
COPY PVPBack.Core/PVPBack.Core.csproj PVPBack.Core/
COPY PVPBack.Domain/PVPBack.Domain.csproj PVPBack.Domain/
COPY PVPBack.Infrastructure/PVPBack.Infrastructure.csproj PVPBack.Infrastructure/

RUN dotnet restore PVPBack.Api/PVPBack.Api.csproj

# Copy the rest of the source
COPY . .

RUN dotnet publish PVPBack.Api/PVPBack.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:10000

EXPOSE 10000

ENTRYPOINT ["dotnet", "PVPBack.Api.dll"]