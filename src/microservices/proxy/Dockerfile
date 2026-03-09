FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8000

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["ProxyService/ProxyService.csproj", "ProxyService/"]
RUN dotnet restore "ProxyService/ProxyService.csproj"
COPY . .
RUN dotnet build "ProxyService/ProxyService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ProxyService/ProxyService.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ProxyService.dll"]
