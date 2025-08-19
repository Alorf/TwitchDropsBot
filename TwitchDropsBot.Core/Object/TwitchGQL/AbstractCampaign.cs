using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDropsBot.Core.Object.TwitchGQL
{
    public abstract class AbstractCampaign
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public Game? Game { get; set; }
        public string? Status { get; set; }
        public List<TimeBasedDrop> TimeBasedDrops { get; set; }
        public Allow Allow { get; set; }
        public abstract Task NotifiateAsync(TwitchUser twitchUser);
    }
}
