using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDropsBot.Core.Object.TwitchGQL
{
    public interface IinventorySystem
    {

        public string GetName();
        public string GetImage();
        public string GetGroup();
        public string GetStatus();

    }
}
