namespace TwitchDropsBot.WinForms
{
    partial class TwitchUserTab
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            twitchLoggerTextBox = new TextBox();
            tempTabControl = new TabControl();
            currentTabPage = new TabPage();
            Bot = new GroupBox();
            tableLayoutPanel1 = new TableLayoutPanel();
            labelMinRemaining = new Label();
            labelPercentage = new Label();
            ReloadButton = new Button();
            labelDrop = new Label();
            labelGame = new Label();
            progressBar = new ProgressBar();
            groupBox2 = new GroupBox();
            inventoryListView = new ListView();
            dropsInventoryImageList = new ImageList(components);
            gameImageList = new ImageList(components);
            tempTabControl.SuspendLayout();
            currentTabPage.SuspendLayout();
            Bot.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            groupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // twitchLoggerTextBox
            // 
            twitchLoggerTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            twitchLoggerTextBox.Location = new Point(6, 15);
            twitchLoggerTextBox.Multiline = true;
            twitchLoggerTextBox.Name = "twitchLoggerTextBox";
            twitchLoggerTextBox.ReadOnly = true;
            twitchLoggerTextBox.ScrollBars = ScrollBars.Vertical;
            twitchLoggerTextBox.Size = new Size(578, 324);
            twitchLoggerTextBox.TabIndex = 1;
            // 
            // tempTabControl
            // 
            tempTabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tempTabControl.Controls.Add(currentTabPage);
            tempTabControl.Location = new Point(0, 0);
            tempTabControl.Name = "tempTabControl";
            tempTabControl.SelectedIndex = 0;
            tempTabControl.Size = new Size(853, 534);
            tempTabControl.TabIndex = 2;
            // 
            // currentTabPage
            // 
            currentTabPage.Controls.Add(Bot);
            currentTabPage.Controls.Add(groupBox2);
            currentTabPage.Location = new Point(4, 24);
            currentTabPage.Name = "currentTabPage";
            currentTabPage.Padding = new Padding(3);
            currentTabPage.Size = new Size(845, 506);
            currentTabPage.TabIndex = 0;
            currentTabPage.Text = "tabPage1";
            currentTabPage.UseVisualStyleBackColor = true;
            // 
            // Bot
            // 
            Bot.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Bot.Controls.Add(tableLayoutPanel1);
            Bot.Controls.Add(ReloadButton);
            Bot.Controls.Add(labelDrop);
            Bot.Controls.Add(labelGame);
            Bot.Controls.Add(progressBar);
            Bot.Controls.Add(twitchLoggerTextBox);
            Bot.Dock = DockStyle.Fill;
            Bot.Location = new Point(3, 3);
            Bot.Name = "Bot";
            Bot.Size = new Size(590, 500);
            Bot.TabIndex = 17;
            Bot.TabStop = false;
            Bot.Text = "Bot";
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tableLayoutPanel1.ColumnCount = 3;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34.9603F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.53564F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31.5040569F));
            tableLayoutPanel1.Controls.Add(labelMinRemaining, 2, 0);
            tableLayoutPanel1.Controls.Add(labelPercentage, 1, 0);
            tableLayoutPanel1.Location = new Point(3, 449);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 1;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Size = new Size(581, 19);
            tableLayoutPanel1.TabIndex = 28;
            // 
            // labelMinRemaining
            // 
            labelMinRemaining.Anchor = AnchorStyles.None;
            labelMinRemaining.AutoSize = true;
            labelMinRemaining.Location = new Point(428, 2);
            labelMinRemaining.Name = "labelMinRemaining";
            labelMinRemaining.Size = new Size(121, 15);
            labelMinRemaining.TabIndex = 24;
            labelMinRemaining.Text = "Minutes remaining : -";
            // 
            // labelPercentage
            // 
            labelPercentage.Anchor = AnchorStyles.Top | AnchorStyles.Bottom;
            labelPercentage.AutoSize = true;
            labelPercentage.Location = new Point(287, 0);
            labelPercentage.Name = "labelPercentage";
            labelPercentage.Size = new Size(25, 19);
            labelPercentage.TabIndex = 29;
            labelPercentage.Text = "- %";
            labelPercentage.TextAlign = ContentAlignment.MiddleRight;
            // 
            // ReloadButton
            // 
            ReloadButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            ReloadButton.Location = new Point(296, 342);
            ReloadButton.Name = "ReloadButton";
            ReloadButton.Size = new Size(288, 35);
            ReloadButton.TabIndex = 27;
            ReloadButton.Text = "Reload";
            ReloadButton.UseVisualStyleBackColor = true;
            ReloadButton.Click += ReloadButton_Click;
            // 
            // labelDrop
            // 
            labelDrop.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            labelDrop.Location = new Point(6, 388);
            labelDrop.Name = "labelDrop";
            labelDrop.Size = new Size(284, 35);
            labelDrop.TabIndex = 25;
            labelDrop.Text = "Drop : N/A";
            labelDrop.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // labelGame
            // 
            labelGame.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            labelGame.Location = new Point(6, 342);
            labelGame.Name = "labelGame";
            labelGame.Size = new Size(284, 35);
            labelGame.TabIndex = 24;
            labelGame.Text = "Game : N/A";
            labelGame.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // progressBar
            // 
            progressBar.Dock = DockStyle.Bottom;
            progressBar.Location = new Point(3, 474);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(584, 23);
            progressBar.TabIndex = 22;
            // 
            // groupBox2
            // 
            groupBox2.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            groupBox2.Controls.Add(inventoryListView);
            groupBox2.Dock = DockStyle.Right;
            groupBox2.Location = new Point(593, 3);
            groupBox2.Name = "groupBox2";
            groupBox2.RightToLeft = RightToLeft.Yes;
            groupBox2.Size = new Size(249, 500);
            groupBox2.TabIndex = 16;
            groupBox2.TabStop = false;
            groupBox2.Text = "Inventory";
            // 
            // inventoryListView
            // 
            inventoryListView.Alignment = ListViewAlignment.Left;
            inventoryListView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            inventoryListView.BorderStyle = BorderStyle.None;
            inventoryListView.FullRowSelect = true;
            inventoryListView.HeaderStyle = ColumnHeaderStyle.None;
            inventoryListView.Location = new Point(6, 15);
            inventoryListView.MultiSelect = false;
            inventoryListView.Name = "inventoryListView";
            inventoryListView.RightToLeft = RightToLeft.No;
            inventoryListView.ShowItemToolTips = true;
            inventoryListView.Size = new Size(240, 479);
            inventoryListView.TabIndex = 0;
            inventoryListView.UseCompatibleStateImageBehavior = false;
            inventoryListView.View = View.Details;
            // 
            // dropsInventoryImageList
            // 
            dropsInventoryImageList.ColorDepth = ColorDepth.Depth32Bit;
            dropsInventoryImageList.ImageSize = new Size(16, 16);
            dropsInventoryImageList.TransparentColor = Color.Transparent;
            // 
            // gameImageList
            // 
            gameImageList.ColorDepth = ColorDepth.Depth32Bit;
            gameImageList.ImageSize = new Size(16, 16);
            gameImageList.TransparentColor = Color.Transparent;
            // 
            // TwitchUserTab
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(tempTabControl);
            Name = "TwitchUserTab";
            Size = new Size(853, 534);
            tempTabControl.ResumeLayout(false);
            currentTabPage.ResumeLayout(false);
            Bot.ResumeLayout(false);
            Bot.PerformLayout();
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            groupBox2.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TextBox twitchLoggerTextBox;
        private TabControl tempTabControl;
        private TabPage currentTabPage;
        private GroupBox groupBox2;
        private ImageList dropsInventoryImageList;
        private GroupBox Bot;
        private Button ReloadButton;
        private Label labelDrop;
        private Label labelGame;
        private ProgressBar progressBar;
        private TableLayoutPanel tableLayoutPanel1;
        private Label labelPercentage;
        private Label labelMinRemaining;
        private ListView inventoryListView;
        private ImageList gameImageList;
    }
}