using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace TwitchDropsBot.Core.Object.TwitchGQL
{
    public interface IInventorySystem
    {
        public string Id { get; set; }
        public string GetName();
        public string GetImage();
        public string GetGroup();
        public string GetStatus();
        public string? GetGameImageUrl(int size);
        public string? GetGameSlug();

    }
}
