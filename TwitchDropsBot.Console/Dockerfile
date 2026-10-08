# aspnet rather than runtime: the console app hosts the optional web UI (WEBUI_ENABLED)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

RUN apt-get update && apt-get install -y \
    curl gnupg && \
    curl https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -o packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    apt-get update && apt-get install -y libmsquic && \
    rm -f packages-microsoft-prod.deb

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
ENV INSIDE_DOCKER=true

# The web UI stays off unless WEBUI_ENABLED=true is set; see TwitchDropsBot.Web/README.md
ENV WEBUI_ENABLED=false

VOLUME ["/app/Configuration"]

# The web UI's port, when it is on
EXPOSE 8080

ENTRYPOINT ["dotnet", "TwitchDropsBot.Console.dll"]