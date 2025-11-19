# TwitchDropsBot

This project is inspired by [TwitchDropsMiner](https://github.com/DevilXD/TwitchDropsMiner/). I used it on Linux but faced several compatibility issues, so I decided to create my own solution.

The main goal of this project is to help users claim their Twitch and Kick drops when they are unable to watch streams. For example, in Europe, some drops are hard to get because they require watching American streams late at night (e.g., Rust).

> [!WARNING]  
> This bot is still a work in progress. Sometimes it may fail to collect drops due to frequent updates to Twitch's backend.

## Features
- [x] Support for Twitch and Kick **Campaigns** (Rust) and Twitch **Reward Campaigns** (Minecraft items)
- [x] Manage multiple Twitch and Kick accounts
- [x] Priority list
- [x] Discord Webhook support
- [x] Easy GQL updates using [Postman](https://www.postman.com/) _(see the Postman section)_

### GUI Version (Only for Twitch)

- [x] Tray icon
- [x] Manage your priority list
- [x] Inventory section showing your inventory and current campaigns
- [x] Button to add new accounts

### Console Version

- [x] Fully functional!
- [x] Add account feature (start the console with the parameter `--add-account`)

## Getting Started

### Docker

A docker image is available to pull if you want

docker compose example :
```yml
services:
  twitchdropsbot:
    container_name: TwitchDropsBot
    image: ghcr.io/alorf/twitchdropsbot:latest
    volumes:
      - ./config:/app/Configuration
      - ./logs:/app/logs
    environment:
      - ADD_ACCOUNT=false
      - INSIDE_DOCKER=true
    restart: unless-stopped
```
You need to interact at least once with the bot to add one account. You can achieve this by using this command
```bash
docker compose run --rm twitchdropsbot
```

If you encounter any issue like `Access to the path '/app/Configuration/config.json' is denied.` or empty logs folder.

Check the permission of each volume.

After, you can close the bot and start it by using this command 
```bash
docker compose up -d
```

### Compiled Version

Whenever I consider a new version ready for release, I publish it on the [Releases](https://github.com/Alorf/TwitchDropsBot/releases) page.

#### Installing msquic

Msquic is needed to run the app since Kick need HTTP version 3.0 to work. 

Installation example for debian 12:
```bash
sudo apt update && sudo apt install -y curl gnupg apt-transport-https ca-certificates && \
curl -sSL https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -o packages-microsoft-prod.deb && \
sudo dpkg -i packages-microsoft-prod.deb && rm packages-microsoft-prod.deb && \
sudo apt update && sudo apt install -y libmsquic
```
see [here](https://learn.microsoft.com/en-us/linux/packages) if you want more information about linux packages

#### Usage

1. Download the bot.
2. Launch the bot.
3. Log in.
4. Close the bot.
5. Update the config file.
6. Relaunch the bot.
7. You're done!

#### Updates

1. Download the bot.
2. Copy and paste your config file.
3. You're done!

## Postman

Twitch frequently changes the hashes for GQL operations used by the bot.  
I’ve created a Postman collection that contains all the required GQL requests.  
If the bot stops working, you can replace the hashes using this collection.  
Once updated, simply export your collection and replace the old one.

> [!NOTE]  
> The updated collection must have the same name as the original one.

## Contributing

This app was built using [Visual Studio](https://visualstudio.microsoft.com) and [Rider](https://www.jetbrains.com/rider/).
We welcome contributions! To contribute, follow these steps:

1. Fork the repository.
2. Create a new branch: `git checkout -b feature/YourFeature`.
3. Commit your changes: `git commit -am 'Add some feature'`.
4. Push the branch: `git push origin feature/YourFeature`.
5. Open a pull request.

## License

This project is licensed under the MIT License. See the [LICENSE](https://github.com/Alorf/TwitchDropsBot/blob/master/LICENSE.txt) file for details.

> **Disclaimer**
> This project is provided for educational purposes and personal use only. Use it at your own risk. By using this project, you acknowledge that you have been warned. The author cannot be held responsible for any suspension, limitation, or banning of your accounts. Issues related to account limitations, suspensions, or bans will not be considered. Any use for automated farming or bot farms is strictly discouraged.