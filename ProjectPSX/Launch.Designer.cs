namespace ProjectPSX
{
    partial class Launch
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
            this.lblBios = new System.Windows.Forms.Label();
            this.fileDialog = new System.Windows.Forms.OpenFileDialog();
            this.btnSelectBios = new System.Windows.Forms.Button();
            this.lblBiosLocation = new System.Windows.Forms.Label();
            this.lblCDLocation = new System.Windows.Forms.Label();
            this.btnSelectCD = new System.Windows.Forms.Button();
            this.lblCD = new System.Windows.Forms.Label();
            this.btnStart = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblBios
            // 
            this.lblBios.AutoSize = true;
            this.lblBios.Location = new System.Drawing.Point(28, 30);
            this.lblBios.Name = "lblBios";
            this.lblBios.Size = new System.Drawing.Size(52, 13);
            this.lblBios.TabIndex = 0;
            this.lblBios.Text = "Bios Path";
            this.lblBios.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // fileDialog
            // 
            this.fileDialog.FileName = "openFileDialog1";
            // 
            // btnSelectBios
            // 
            this.btnSelectBios.Location = new System.Drawing.Point(96, 25);
            this.btnSelectBios.Name = "btnSelectBios";
            this.btnSelectBios.Size = new System.Drawing.Size(75, 23);
            this.btnSelectBios.TabIndex = 1;
            this.btnSelectBios.Text = "Select Bios";
            this.btnSelectBios.UseVisualStyleBackColor = true;
            this.btnSelectBios.Click += new System.EventHandler(this.BtnSelectBios_Click);
            // 
            // lblBiosLocation
            // 
            this.lblBiosLocation.AutoSize = true;
            this.lblBiosLocation.Location = new System.Drawing.Point(28, 64);
            this.lblBiosLocation.Name = "lblBiosLocation";
            this.lblBiosLocation.Size = new System.Drawing.Size(27, 13);
            this.lblBiosLocation.TabIndex = 2;
            this.lblBiosLocation.Text = "Bios";
            this.lblBiosLocation.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // lblCDLocation
            // 
            this.lblCDLocation.AutoSize = true;
            this.lblCDLocation.Location = new System.Drawing.Point(28, 162);
            this.lblCDLocation.Name = "lblCDLocation";
            this.lblCDLocation.Size = new System.Drawing.Size(27, 13);
            this.lblCDLocation.TabIndex = 5;
            this.lblCDLocation.Text = "Bios";
            this.lblCDLocation.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // btnSelectCD
            // 
            this.btnSelectCD.Location = new System.Drawing.Point(96, 119);
            this.btnSelectCD.Name = "btnSelectCD";
            this.btnSelectCD.Size = new System.Drawing.Size(75, 23);
            this.btnSelectCD.TabIndex = 4;
            this.btnSelectCD.Text = "select Cd";
            this.btnSelectCD.UseVisualStyleBackColor = true;
            this.btnSelectCD.Click += new System.EventHandler(this.BtnSelectCD_Click);
            // 
            // lblCD
            // 
            this.lblCD.AutoSize = true;
            this.lblCD.Location = new System.Drawing.Point(28, 124);
            this.lblCD.Name = "lblCD";
            this.lblCD.Size = new System.Drawing.Size(45, 13);
            this.lblCD.TabIndex = 3;
            this.lblCD.Text = "Cd Path";
            this.lblCD.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(197, 226);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(75, 23);
            this.btnStart.TabIndex = 6;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.BtnStart_Click);
            // 
            // Launch
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.lblCDLocation);
            this.Controls.Add(this.btnSelectCD);
            this.Controls.Add(this.lblCD);
            this.Controls.Add(this.lblBiosLocation);
            this.Controls.Add(this.btnSelectBios);
            this.Controls.Add(this.lblBios);
            this.Name = "Launch";
            this.Text = "Launch";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblBios;
        private System.Windows.Forms.OpenFileDialog fileDialog;
        private System.Windows.Forms.Button btnSelectBios;
        private System.Windows.Forms.Label lblBiosLocation;
        private System.Windows.Forms.Label lblCDLocation;
        private System.Windows.Forms.Button btnSelectCD;
        private System.Windows.Forms.Label lblCD;
        private System.Windows.Forms.Button btnStart;
    }
}