FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CentralMonitoring.Api/CentralMonitoring.Api.csproj CentralMonitoring.Api/
COPY CentralMonitoring.Infrastructure/CentralMonitoring.Infrastructure.csproj CentralMonitoring.Infrastructure/
COPY CentralMonitoring.Domain/CentralMonitoring.Domain.csproj CentralMonitoring.Domain/
COPY CentralMonitoring.Shared/CentralMonitoring.Shared.csproj CentralMonitoring.Shared/
RUN dotnet restore CentralMonitoring.Api/CentralMonitoring.Api.csproj

COPY . .
RUN dotnet publish CentralMonitoring.Api/CentralMonitoring.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "CentralMonitoring.Api.dll"]
