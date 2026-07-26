# 🔧 Rust Null Minutes Fix

## Проблема
При фарме дропов Rust на аккаунтах без привязки Steam/Amazon, Twitch API возвращает null в поле minutesWatched. Оригинальный бот падает с ошибкой: System.Text.Json.JsonException: The JSON value could not be converted to System.Int32. Path: $.data.channelDropCampaignsProgress[0].rewardGroups[0].progressCriteria.requirements.minutesWatched Cannot get the value of a token type 'Null' as a number.

## Решение
Добавлен try-catch вокруг вызова DoGQLRequestAsync в методе FetchDropCampaignsProgressAsync. При возникновении JsonException возвращается пустой список вместо падения бота.

## Использование
git clone https://github.com/keronis7/TwitchDropsBot.git
cd TwitchDropsBot
docker build -f TwitchDropsBot.Console/Dockerfile -t twitchdropsbot:fixed .
docker run -d --name twitchminer --restart always -v /path/to/config:/app/Configuration twitchdropsbot:fixed

## Конфиг для Rust
{
  "TwitchSettings": {
    "TwitchUsers": [{
      "ClientSecret": "твой_client_secret",
      "UniqueId": "твой_unique_id",
      "Login": "твой_логин",
      "Id": "твой_id",
      "Enabled": true,
      "FavouriteGames": ["Rust"]
    }],
    "OnlyFavouriteGames": true,
    "FavouriteGames": ["Rust"]
  }
}

## Pull Request
Фикс отправлен в оригинальный репозиторий: https://github.com/Alorf/TwitchDropsBot/pulls

## Статус
✅ Работает на 11 контейнерах | ✅ Фармит Rust без ошибок | ✅ Docker ready
