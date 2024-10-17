using System.Windows.Forms;

namespace TwitchDropsBot.WinForms
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            tabControl1 = new TabControl();
            notifyIcon1 = new NotifyIcon(components);
            contextMenuStrip1 = new ContextMenuStrip(components);
            exitToolStripMenuItem = new ToolStripMenuItem();
            groupBox1 = new GroupBox();
            checkBoxConnectedAccounts = new CheckBox();
            checkBoxMinimizeInTray = new CheckBox();
            buttonUp = new Button();
            buttonDown = new Button();
            buttonAdd = new Button();
            textBoxNameOfGame = new TextBox();
            FavGameListBox = new ListBox();
            buttonDelete = new Button();
            buttonAddNewAccount = new Button();
            buttonPutInTray = new Button();
            checkBoxFavourite = new CheckBox();
            checkBoxStartup = new CheckBox();
            tabControl1.SuspendLayout();
            contextMenuStrip1.SuspendLayout();
            groupBox1.SuspendLayout();
            SuspendLayout();
            // 
            // tabControl1
            // 
            tabControl1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControl1.Location = new Point(207, 0);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(853, 534);
            tabControl1.TabIndex = 1;
            // 
            // notifyIcon1
            // 
            notifyIcon1.ContextMenuStrip = contextMenuStrip1;
            notifyIcon1.Icon = (Icon)resources.GetObject("notifyIcon1.Icon");
            notifyIcon1.Text = "notifyIcon1";
            notifyIcon1.MouseClick += notifyIcon1_MouseClick;
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.Items.AddRange(new ToolStripItem[] { exitToolStripMenuItem });
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new Size(94, 26);
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new Size(93, 22);
            exitToolStripMenuItem.Text = "Exit";
            exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(checkBoxConnectedAccounts);
            groupBox1.Controls.Add(checkBoxMinimizeInTray);
            groupBox1.Controls.Add(buttonUp);
            groupBox1.Controls.Add(buttonDown);
            groupBox1.Controls.Add(buttonAdd);
            groupBox1.Controls.Add(textBoxNameOfGame);
            groupBox1.Controls.Add(FavGameListBox);
            groupBox1.Controls.Add(buttonDelete);
            groupBox1.Controls.Add(buttonAddNewAccount);
            groupBox1.Controls.Add(buttonPutInTray);
            groupBox1.Controls.Add(checkBoxFavourite);
            groupBox1.Controls.Add(checkBoxStartup);
            groupBox1.Dock = DockStyle.Left;
            groupBox1.Location = new Point(0, 0);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(201, 534);
            groupBox1.TabIndex = 2;
            groupBox1.TabStop = false;
            groupBox1.Text = "Settings";
            // 
            // checkBoxConnectedAccounts
            // 
            checkBoxConnectedAccounts.AutoSize = true;
            checkBoxConnectedAccounts.Location = new Point(6, 80);
            checkBoxConnectedAccounts.Name = "checkBoxConnectedAccounts";
            checkBoxConnectedAccounts.Size = new Size(161, 19);
            checkBoxConnectedAccounts.TabIndex = 14;
            checkBoxConnectedAccounts.Text = "Only connected accounts";
            checkBoxConnectedAccounts.UseVisualStyleBackColor = true;
            checkBoxConnectedAccounts.CheckedChanged += checkBoxConnectedAccounts_CheckedChanged;
            // 
            // checkBoxMinimizeInTray
            // 
            checkBoxMinimizeInTray.AutoSize = true;
            checkBoxMinimizeInTray.Location = new Point(6, 105);
            checkBoxMinimizeInTray.Name = "checkBoxMinimizeInTray";
            checkBoxMinimizeInTray.Size = new Size(149, 19);
            checkBoxMinimizeInTray.TabIndex = 13;
            checkBoxMinimizeInTray.Text = "Put in tray on minimize";
            checkBoxMinimizeInTray.UseVisualStyleBackColor = true;
            checkBoxMinimizeInTray.CheckedChanged += CheckBoxMinimizeInTrayCheckedChanged;
            // 
            // buttonUp
            // 
            buttonUp.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            buttonUp.Location = new Point(6, 441);
            buttonUp.Name = "buttonUp";
            buttonUp.Size = new Size(60, 23);
            buttonUp.TabIndex = 12;
            buttonUp.Text = "Up";
            buttonUp.UseVisualStyleBackColor = true;
            buttonUp.Click += buttonUp_Click;
            // 
            // buttonDown
            // 
            buttonDown.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            buttonDown.Location = new Point(70, 441);
            buttonDown.Name = "buttonDown";
            buttonDown.Size = new Size(60, 23);
            buttonDown.TabIndex = 11;
            buttonDown.Text = "Down";
            buttonDown.UseVisualStyleBackColor = true;
            buttonDown.Click += buttonDown_Click;
            // 
            // buttonAdd
            // 
            buttonAdd.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            buttonAdd.Location = new Point(118, 237);
            buttonAdd.Name = "buttonAdd";
            buttonAdd.Size = new Size(75, 23);
            buttonAdd.TabIndex = 10;
            buttonAdd.Text = "Add";
            buttonAdd.UseVisualStyleBackColor = true;
            buttonAdd.Click += buttonAdd_Click;
            // 
            // textBoxNameOfGame
            // 
            textBoxNameOfGame.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            textBoxNameOfGame.Location = new Point(6, 237);
            textBoxNameOfGame.Name = "textBoxNameOfGame";
            textBoxNameOfGame.Size = new Size(106, 23);
            textBoxNameOfGame.TabIndex = 9;
            // 
            // FavGameListBox
            // 
            FavGameListBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            FavGameListBox.FormattingEnabled = true;
            FavGameListBox.ItemHeight = 15;
            FavGameListBox.Location = new Point(6, 266);
            FavGameListBox.Name = "FavGameListBox";
            FavGameListBox.Size = new Size(189, 169);
            FavGameListBox.TabIndex = 8;
            // 
            // buttonDelete
            // 
            buttonDelete.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            buttonDelete.Location = new Point(134, 441);
            buttonDelete.Name = "buttonDelete";
            buttonDelete.Size = new Size(60, 23);
            buttonDelete.TabIndex = 7;
            buttonDelete.Text = "Delete";
            buttonDelete.UseVisualStyleBackColor = true;
            buttonDelete.Click += buttonDelete_Click;
            // 
            // buttonAddNewAccount
            // 
            buttonAddNewAccount.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            buttonAddNewAccount.Location = new Point(6, 470);
            buttonAddNewAccount.Name = "buttonAddNewAccount";
            buttonAddNewAccount.Size = new Size(189, 23);
            buttonAddNewAccount.TabIndex = 5;
            buttonAddNewAccount.Text = "Add new account";
            buttonAddNewAccount.UseVisualStyleBackColor = true;
            buttonAddNewAccount.Click += buttonAddNewAccount_Click;
            // 
            // buttonPutInTray
            // 
            buttonPutInTray.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            buttonPutInTray.Location = new Point(6, 499);
            buttonPutInTray.Name = "buttonPutInTray";
            buttonPutInTray.Size = new Size(189, 23);
            buttonPutInTray.TabIndex = 4;
            buttonPutInTray.Text = "Put In tray";
            buttonPutInTray.UseVisualStyleBackColor = true;
            buttonPutInTray.Click += buttonPutInTray_Click;
            // 
            // checkBoxFavourite
            // 
            checkBoxFavourite.AutoSize = true;
            checkBoxFavourite.Location = new Point(6, 55);
            checkBoxFavourite.Name = "checkBoxFavourite";
            checkBoxFavourite.Size = new Size(144, 19);
            checkBoxFavourite.TabIndex = 3;
            checkBoxFavourite.Text = "Only favourites games";
            checkBoxFavourite.UseVisualStyleBackColor = true;
            checkBoxFavourite.CheckedChanged += checkBoxFavourite_CheckedChanged;
            // 
            // checkBoxStartup
            // 
            checkBoxStartup.AutoSize = true;
            checkBoxStartup.Location = new Point(6, 32);
            checkBoxStartup.Name = "checkBoxStartup";
            checkBoxStartup.Size = new Size(122, 19);
            checkBoxStartup.TabIndex = 2;
            checkBoxStartup.Text = "Launch on startup";
            checkBoxStartup.UseVisualStyleBackColor = true;
            checkBoxStartup.CheckedChanged += checkBoxStartup_CheckedChanged;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1060, 534);
            Controls.Add(groupBox1);
            Controls.Add(tabControl1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MinimumSize = new Size(1076, 573);
            Name = "MainForm";
            Text = "TwitchDropsBot";
            Load += Form1_Load;
            Resize += Form1_Resize;
            tabControl1.ResumeLayout(false);
            contextMenuStrip1.ResumeLayout(false);
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private TabControl tabControl1;
        private NotifyIcon notifyIcon1;
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem exitToolStripMenuItem;
        private GroupBox groupBox1;
        private CheckBox checkBoxFavourite;
        private CheckBox checkBoxStartup;
        private Button buttonPutInTray;
        private Button buttonAddNewAccount;
        private Button buttonDelete;
        private ListBox FavGameListBox;
        private Button buttonAdd;
        private TextBox textBoxNameOfGame;
        private Button buttonUp;
        private Button buttonDown;
        private CheckBox checkBoxMinimizeInTray;
        private CheckBox checkBoxConnectedAccounts;
    }
}
