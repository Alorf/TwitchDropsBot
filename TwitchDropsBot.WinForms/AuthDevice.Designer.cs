namespace TwitchDropsBot.WinForms
{
    partial class AuthDevice
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            linkLabel1 = new LinkLabel();
            AuthStatus = new Label();
            AuthCode = new Label();
            SuspendLayout();
            // 
            // linkLabel1
            // 
            linkLabel1.AutoSize = true;
            linkLabel1.Location = new Point(65, 9);
            linkLabel1.Name = "linkLabel1";
            linkLabel1.Size = new Size(169, 15);
            linkLabel1.TabIndex = 0;
            linkLabel1.TabStop = true;
            linkLabel1.Text = "https://www.twitch.tv/activate";
            linkLabel1.LinkClicked += linkLabel1_LinkClicked;
            // 
            // AuthStatus
            // 
            AuthStatus.AutoSize = true;
            AuthStatus.Location = new Point(65, 123);
            AuthStatus.Name = "AuthStatus";
            AuthStatus.Size = new Size(183, 15);
            AuthStatus.TabIndex = 1;
            AuthStatus.Text = "Waiting for user to authenticate...";
            // 
            // AuthCode
            // 
            AuthCode.AutoSize = true;
            AuthCode.Location = new Point(65, 61);
            AuthCode.Name = "AuthCode";
            AuthCode.Size = new Size(35, 15);
            AuthCode.TabIndex = 2;
            AuthCode.Text = "Code";
            // 
            // AuthDevice
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(309, 199);
            Controls.Add(AuthCode);
            Controls.Add(AuthStatus);
            Controls.Add(linkLabel1);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            Name = "AuthDevice";
            ShowInTaskbar = false;
            Text = "AuthDevice";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private LinkLabel linkLabel1;
        private Label AuthStatus;
        private Label AuthCode;
    }
}