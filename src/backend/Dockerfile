# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["ExpatOne.Api/ExpatOne.Api.csproj", "ExpatOne.Api/"]
COPY ["ExpatOne.Application/ExpatOne.Application.csproj", "ExpatOne.Application/"]
COPY ["ExpatOne.Domain/ExpatOne.Domain.csproj", "ExpatOne.Domain/"]
COPY ["ExpatOne.Infrastructure/ExpatOne.Infrastructure.csproj", "ExpatOne.Infrastructure/"]

RUN dotnet restore "ExpatOne.Api/ExpatOne.Api.csproj"

COPY . .

WORKDIR "/src/ExpatOne.Api"
RUN dotnet publish "ExpatOne.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ExpatOne.Api.dll"]
