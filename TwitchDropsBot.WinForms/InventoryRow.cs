using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TwitchDropsBot.Core.Object.TwitchGQL;

namespace TwitchDropsBot.WinForms
{
    public partial class InventoryRow : UserControl
    {
        public InventoryRow(GameEventDrop ged)
        {
            InitializeComponent();

            picture.Load(ged.ImageURL);
            titleLabel.Text = ged.Name;
            statusLabel.Text = "claimed";
        }
    }
}
