FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CentralMonitoring.CloudApi/CentralMonitoring.CloudApi.csproj CentralMonitoring.CloudApi/
RUN dotnet restore CentralMonitoring.CloudApi/CentralMonitoring.CloudApi.csproj

COPY . .
RUN dotnet publish CentralMonitoring.CloudApi/CentralMonitoring.CloudApi.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 10000

ENTRYPOINT ["dotnet", "CentralMonitoring.CloudApi.dll"]
