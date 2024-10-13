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
            twitchLoggerTextBox = new TextBox();
            tempTabControl = new TabControl();
            currentTabPage = new TabPage();
            groupBox2 = new GroupBox();
            flowLayoutPanel1 = new CustomFlowLayoutPanel();
            ReloadButton = new Button();
            labelMinRemaining = new Label();
            labelDrop = new Label();
            labelGame = new Label();
            labelPercentage = new Label();
            progressBar = new ProgressBar();
            tempTabControl.SuspendLayout();
            currentTabPage.SuspendLayout();
            groupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // twitchLoggerTextBox
            // 
            twitchLoggerTextBox.Location = new Point(6, 6);
            twitchLoggerTextBox.Multiline = true;
            twitchLoggerTextBox.Name = "twitchLoggerTextBox";
            twitchLoggerTextBox.ReadOnly = true;
            twitchLoggerTextBox.Size = new Size(578, 328);
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
            currentTabPage.Controls.Add(groupBox2);
            currentTabPage.Controls.Add(ReloadButton);
            currentTabPage.Controls.Add(labelMinRemaining);
            currentTabPage.Controls.Add(labelDrop);
            currentTabPage.Controls.Add(labelGame);
            currentTabPage.Controls.Add(labelPercentage);
            currentTabPage.Controls.Add(twitchLoggerTextBox);
            currentTabPage.Controls.Add(progressBar);
            currentTabPage.Location = new Point(4, 24);
            currentTabPage.Name = "currentTabPage";
            currentTabPage.Padding = new Padding(3);
            currentTabPage.Size = new Size(845, 506);
            currentTabPage.TabIndex = 0;
            currentTabPage.Text = "tabPage1";
            currentTabPage.UseVisualStyleBackColor = true;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(flowLayoutPanel1);
            groupBox2.Location = new Point(590, 0);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(256, 506);
            groupBox2.TabIndex = 16;
            groupBox2.TabStop = false;
            groupBox2.Text = "Inventory";
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.AutoScroll = true;
            flowLayoutPanel1.FlowDirection = FlowDirection.TopDown;
            flowLayoutPanel1.Location = new Point(6, 22);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.RightToLeft = RightToLeft.No;
            flowLayoutPanel1.Size = new Size(243, 476);
            flowLayoutPanel1.TabIndex = 0;
            flowLayoutPanel1.WrapContents = false;
            // 
            // ReloadButton
            // 
            ReloadButton.Location = new Point(296, 349);
            ReloadButton.Name = "ReloadButton";
            ReloadButton.Size = new Size(288, 35);
            ReloadButton.TabIndex = 15;
            ReloadButton.Text = "Reload";
            ReloadButton.UseVisualStyleBackColor = true;
            ReloadButton.Click += ReloadButton_Click;
            // 
            // labelMinRemaining
            // 
            labelMinRemaining.Location = new Point(334, 453);
            labelMinRemaining.Name = "labelMinRemaining";
            labelMinRemaining.Size = new Size(250, 19);
            labelMinRemaining.TabIndex = 6;
            labelMinRemaining.Text = "Minutes remaining : -";
            labelMinRemaining.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // labelDrop
            // 
            labelDrop.Location = new Point(6, 395);
            labelDrop.Name = "labelDrop";
            labelDrop.Size = new Size(284, 35);
            labelDrop.TabIndex = 5;
            labelDrop.Text = "Drop : N/A";
            labelDrop.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // labelGame
            // 
            labelGame.Location = new Point(6, 349);
            labelGame.Name = "labelGame";
            labelGame.Size = new Size(284, 35);
            labelGame.TabIndex = 4;
            labelGame.Text = "Game : N/A";
            labelGame.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // labelPercentage
            // 
            labelPercentage.Location = new Point(6, 453);
            labelPercentage.Name = "labelPercentage";
            labelPercentage.Size = new Size(578, 16);
            labelPercentage.TabIndex = 3;
            labelPercentage.Text = "-%";
            labelPercentage.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // progressBar
            // 
            progressBar.Location = new Point(6, 475);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(578, 23);
            progressBar.TabIndex = 0;
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
            currentTabPage.PerformLayout();
            groupBox2.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TextBox twitchLoggerTextBox;
        private TabControl tempTabControl;
        private TabPage currentTabPage;
        private GroupBox groupBox2;
        private Button ReloadButton;
        private Label labelMinRemaining;
        private Label labelDrop;
        private Label labelGame;
        private Label labelPercentage;
        private ProgressBar progressBar;
        private CustomFlowLayoutPanel flowLayoutPanel1;
    }
}