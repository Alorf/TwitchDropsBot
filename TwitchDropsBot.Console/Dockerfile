FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["TwitchDropsBot.Console/TwitchDropsBot.Console.csproj", "TwitchDropsBot.Console/"]
RUN dotnet restore "TwitchDropsBot.Console/TwitchDropsBot.Console.csproj"
COPY . .
WORKDIR "/src/TwitchDropsBot.Console"
RUN dotnet build "TwitchDropsBot.Console.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "TwitchDropsBot.Console.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TwitchDropsBot.Console.dll"]
