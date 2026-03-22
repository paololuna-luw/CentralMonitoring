FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CentralMonitoring.Worker/CentralMonitoring.Worker.csproj CentralMonitoring.Worker/
COPY CentralMonitoring.Infrastructure/CentralMonitoring.Infrastructure.csproj CentralMonitoring.Infrastructure/
COPY CentralMonitoring.Domain/CentralMonitoring.Domain.csproj CentralMonitoring.Domain/
COPY CentralMonitoring.Shared/CentralMonitoring.Shared.csproj CentralMonitoring.Shared/
RUN dotnet restore CentralMonitoring.Worker/CentralMonitoring.Worker.csproj

COPY . .
RUN dotnet publish CentralMonitoring.Worker/CentralMonitoring.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

ENV DOTNET_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CentralMonitoring.Worker.dll"]
