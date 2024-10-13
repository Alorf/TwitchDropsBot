namespace TwitchDropsBot.WinForms
{
    partial class InventoryRow
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
            inventoryTable = new TableLayoutPanel();
            picture = new PictureBox();
            tableLayoutPanel1 = new TableLayoutPanel();
            titleLabel = new Label();
            statusLabel = new Label();
            inventoryTable.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)picture).BeginInit();
            tableLayoutPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // inventoryTable
            // 
            inventoryTable.ColumnCount = 2;
            inventoryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33333F));
            inventoryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66.66667F));
            inventoryTable.Controls.Add(picture, 0, 0);
            inventoryTable.Controls.Add(tableLayoutPanel1, 1, 0);
            inventoryTable.Location = new Point(0, 0);
            inventoryTable.Name = "inventoryTable";
            inventoryTable.RowCount = 1;
            inventoryTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            inventoryTable.Size = new Size(235, 60);
            inventoryTable.TabIndex = 1;
            // 
            // picture
            // 
            picture.Location = new Point(3, 3);
            picture.Name = "picture";
            picture.Size = new Size(72, 54);
            picture.SizeMode = PictureBoxSizeMode.Zoom;
            picture.TabIndex = 2;
            picture.TabStop = false;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Controls.Add(titleLabel, 0, 0);
            tableLayoutPanel1.Controls.Add(statusLabel, 0, 1);
            tableLayoutPanel1.Location = new Point(81, 3);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Size = new Size(151, 54);
            tableLayoutPanel1.TabIndex = 3;
            // 
            // titleLabel
            // 
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(3, 0);
            titleLabel.Name = "titleLabel";
            titleLabel.Size = new Size(131, 27);
            titleLabel.TabIndex = 1;
            titleLabel.Text = "OWCS Home Soldier76 Skin";
            // 
            // statusLabel
            // 
            statusLabel.AutoSize = true;
            statusLabel.Location = new Point(3, 27);
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(94, 15);
            statusLabel.TabIndex = 2;
            statusLabel.Text = "133/260 minutes";
            // 
            // InventoryRow
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(inventoryTable);
            Name = "InventoryRow";
            Size = new Size(235, 60);
            inventoryTable.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)picture).EndInit();
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel inventoryTable;
        private PictureBox picture;
        private TableLayoutPanel tableLayoutPanel1;
        private Label titleLabel;
        private Label statusLabel;
    }
}
