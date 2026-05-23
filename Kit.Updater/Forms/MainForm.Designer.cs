namespace Kit.Updater.Forms
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label       c_TitleLabel;
        private System.Windows.Forms.Label       c_StatusLabel;
        private System.Windows.Forms.ProgressBar c_ProgressBar;
        private System.Windows.Forms.PictureBox  c_BannerImage;
        private System.Windows.Forms.Panel       c_ContentPanel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.c_ContentPanel = new System.Windows.Forms.Panel();
            this.c_ProgressBar  = new System.Windows.Forms.ProgressBar();
            this.c_StatusLabel  = new System.Windows.Forms.Label();
            this.c_TitleLabel   = new System.Windows.Forms.Label();
            this.c_BannerImage  = new System.Windows.Forms.PictureBox();
            this.c_ContentPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.c_BannerImage)).BeginInit();
            this.SuspendLayout();
            // 
            // c_ContentPanel
            // 
            this.c_ContentPanel.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.c_ContentPanel.Controls.Add(this.c_ProgressBar);
            this.c_ContentPanel.Controls.Add(this.c_StatusLabel);
            this.c_ContentPanel.Controls.Add(this.c_TitleLabel);
            this.c_ContentPanel.Controls.Add(this.c_BannerImage);
            this.c_ContentPanel.Dock     = System.Windows.Forms.DockStyle.Fill;
            this.c_ContentPanel.Location = new System.Drawing.Point(0, 0);
            this.c_ContentPanel.Name     = "c_ContentPanel";
            this.c_ContentPanel.Size     = new System.Drawing.Size(500, 285);
            this.c_ContentPanel.TabIndex = 0;
            // 
            // c_ProgressBar
            // 
            this.c_ProgressBar.Anchor                = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.c_ProgressBar.Location              = new System.Drawing.Point(12, 251);
            this.c_ProgressBar.MarqueeAnimationSpeed = 30;
            this.c_ProgressBar.Name                  = "c_ProgressBar";
            this.c_ProgressBar.Size                  = new System.Drawing.Size(476, 22);
            this.c_ProgressBar.Style                 = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.c_ProgressBar.TabIndex              = 3;
            // 
            // c_StatusLabel
            // 
            this.c_StatusLabel.Anchor    = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.c_StatusLabel.BackColor = System.Drawing.Color.Transparent;
            this.c_StatusLabel.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.c_StatusLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            this.c_StatusLabel.Location  = new System.Drawing.Point(12, 209);
            this.c_StatusLabel.Margin    = new System.Windows.Forms.Padding(3, 3, 3, 6);
            this.c_StatusLabel.Name      = "c_StatusLabel";
            this.c_StatusLabel.Size      = new System.Drawing.Size(476, 32);
            this.c_StatusLabel.TabIndex  = 2;
            this.c_StatusLabel.Text      = "Starting...";
            this.c_StatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // c_TitleLabel
            // 
            this.c_TitleLabel.Anchor    = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.c_TitleLabel.BackColor = System.Drawing.Color.Transparent;
            this.c_TitleLabel.Font      = new System.Drawing.Font("Segoe UI Semibold", 16F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.c_TitleLabel.ForeColor = System.Drawing.SystemColors.ControlText;
            this.c_TitleLabel.Location  = new System.Drawing.Point(12, 157);
            this.c_TitleLabel.Name      = "c_TitleLabel";
            this.c_TitleLabel.Size      = new System.Drawing.Size(476, 49);
            this.c_TitleLabel.TabIndex  = 1;
            this.c_TitleLabel.Text      = "Kit Updater";
            this.c_TitleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // c_BannerImage
            // 
            this.c_BannerImage.BackColor = System.Drawing.SystemColors.Highlight;
            this.c_BannerImage.Dock      = System.Windows.Forms.DockStyle.Top;
            this.c_BannerImage.Location  = new System.Drawing.Point(0, 0);
            this.c_BannerImage.Name      = "c_BannerImage";
            this.c_BannerImage.Size      = new System.Drawing.Size(500, 150);
            this.c_BannerImage.SizeMode  = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.c_BannerImage.TabIndex  = 0;
            this.c_BannerImage.TabStop   = false;
            // 
            // BootstrapperForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize            = true;
            this.ClientSize          = new System.Drawing.Size(500, 285);
            this.Controls.Add(this.c_ContentPanel);
            this.Font            = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.Name            = "MainForm";
            this.SizeGripStyle   = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition   = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text            = "Kit Updater";
            this.c_ContentPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.c_BannerImage)).EndInit();
            this.ResumeLayout(false);
        }
    }
}
