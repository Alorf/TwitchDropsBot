# TwitchDropsBot

This project is inspired by [TwitchDropsMiner](https://github.com/DevilXD/TwitchDropsMiner/). I used it on Linux but faced several compatibility issues, so I decided to create my own solution.

The main goal of this project is to help users claim their Twitch drops when they are unable to watch streams. For example, in Europe, some drops are hard to get because they require watching American streams late at night (e.g., Rust).

> [!WARNING]  
> This bot is still a work in progress. Sometimes it may fail to collect drops due to frequent updates to Twitch's backend.  
> If you have any questions, feel free to open a detailed issue.

## Features
- [x] Support for **Campaigns** (Rust) and **Reward Campaigns** (Minecraft items)
- [x] Manage multiple Twitch accounts
- [x] Priority list
- [x] Discord Webhook support
- [x] Easy GQL updates using [Postman](https://www.postman.com/) *(see the Postman section)*

### GUI Version
- [x] Tray icon
- [x] Manage your priority list
- [x] Inventory section showing your inventory and current campaigns
- [x] Button to add new accounts

### Console Version
- [x] Fully functional!
- [ ] Add account feature (upcoming)

## Getting Started
### Compiled Version
Whenever I consider a new version ready for release, I publish it on the [Releases](https://github.com/Alorf/TwitchDropsBot/releases) page.

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
This app was built using [Visual Studio](https://visualstudio.microsoft.com).  
We welcome contributions! To contribute, follow these steps:

1. Fork the repository.
2. Create a new branch: `git checkout -b feature/YourFeature`.
3. Commit your changes: `git commit -am 'Add some feature'`.
4. Push the branch: `git push origin feature/YourFeature`.
5. Open a pull request.

## License
This project is licensed under the MIT License. See the [LICENSE](https://github.com/Alorf/TwitchDropsBot/blob/master/LICENSE.txt) file for details.
