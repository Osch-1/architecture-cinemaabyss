FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8082

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["EventsService/EventsService.csproj", "EventsService/"]
RUN dotnet restore "EventsService/EventsService.csproj"
COPY . .
RUN dotnet build "EventsService/EventsService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EventsService/EventsService.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EventsService.dll"]
