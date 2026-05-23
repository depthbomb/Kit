namespace Kit.Updater.Forms
{
    partial class UpdatePromptForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Panel       _contentPanel;
        private System.Windows.Forms.Label       _messageLabel;
        private System.Windows.Forms.Button      _downloadButton;
        private System.Windows.Forms.Button      _launchCurrentButton;
        private System.Windows.Forms.Button      _skipVersionButton;
        private System.Windows.Forms.Button      _cancelButton;

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
            this._contentPanel        = new System.Windows.Forms.Panel();
            this._cancelButton        = new System.Windows.Forms.Button();
            this._skipVersionButton   = new System.Windows.Forms.Button();
            this._launchCurrentButton = new System.Windows.Forms.Button();
            this._downloadButton      = new System.Windows.Forms.Button();
            this._messageLabel        = new System.Windows.Forms.Label();
            this._contentPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // _contentPanel
            // 
            this._contentPanel.Controls.Add(this._cancelButton);
            this._contentPanel.Controls.Add(this._skipVersionButton);
            this._contentPanel.Controls.Add(this._launchCurrentButton);
            this._contentPanel.Controls.Add(this._downloadButton);
            this._contentPanel.Controls.Add(this._messageLabel);
            this._contentPanel.Dock     = System.Windows.Forms.DockStyle.Fill;
            this._contentPanel.Location = new System.Drawing.Point(0, 0);
            this._contentPanel.Margin   = new System.Windows.Forms.Padding(0);
            this._contentPanel.Name     = "_contentPanel";
            this._contentPanel.Padding  = new System.Windows.Forms.Padding(12);
            this._contentPanel.Size     = new System.Drawing.Size(613, 107);
            this._contentPanel.TabIndex = 0;
            // 
            // _cancelButton
            // 
            this._cancelButton.Anchor                  =  ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._cancelButton.DialogResult            =  System.Windows.Forms.DialogResult.Cancel;
            this._cancelButton.Location                =  new System.Drawing.Point(470, 71);
            this._cancelButton.Name                    =  "_cancelButton";
            this._cancelButton.Size                    =  new System.Drawing.Size(128, 24);
            this._cancelButton.TabIndex                =  6;
            this._cancelButton.Text                    =  "Cancel";
            this._cancelButton.UseVisualStyleBackColor =  true;
            this._cancelButton.Click                   += new System.EventHandler(this.CancelButtonClick);
            // 
            // _skipVersionButton
            // 
            this._skipVersionButton.Anchor                  =  ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this._skipVersionButton.Location                =  new System.Drawing.Point(283, 71);
            this._skipVersionButton.Name                    =  "_skipVersionButton";
            this._skipVersionButton.Size                    =  new System.Drawing.Size(128, 24);
            this._skipVersionButton.TabIndex                =  5;
            this._skipVersionButton.Text                    =  "Skip this version";
            this._skipVersionButton.UseVisualStyleBackColor =  true;
            this._skipVersionButton.Click                   += new System.EventHandler(this.SkipVersionButtonClick);
            // 
            // _launchCurrentButton
            // 
            this._launchCurrentButton.Anchor                  =  ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this._launchCurrentButton.Location                =  new System.Drawing.Point(149, 71);
            this._launchCurrentButton.Name                    =  "_launchCurrentButton";
            this._launchCurrentButton.Size                    =  new System.Drawing.Size(128, 24);
            this._launchCurrentButton.TabIndex                =  4;
            this._launchCurrentButton.Text                    =  "Launch current";
            this._launchCurrentButton.UseVisualStyleBackColor =  true;
            this._launchCurrentButton.Click                   += new System.EventHandler(this.LaunchCurrentButtonClick);
            // 
            // _downloadButton
            // 
            this._downloadButton.Anchor                  =  ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this._downloadButton.Location                =  new System.Drawing.Point(15, 71);
            this._downloadButton.Name                    =  "_downloadButton";
            this._downloadButton.Size                    =  new System.Drawing.Size(128, 24);
            this._downloadButton.TabIndex                =  3;
            this._downloadButton.Text                    =  "Download update";
            this._downloadButton.UseVisualStyleBackColor =  true;
            this._downloadButton.Click                   += new System.EventHandler(this.DownloadButtonClick);
            // 
            // _messageLabel
            // 
            this._messageLabel.Anchor    = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this._messageLabel.Location  = new System.Drawing.Point(15, 18);
            this._messageLabel.Margin    = new System.Windows.Forms.Padding(6);
            this._messageLabel.Name      = "_messageLabel";
            this._messageLabel.Size      = new System.Drawing.Size(583, 44);
            this._messageLabel.TabIndex  = 0;
            this._messageLabel.Text      = "Message";
            this._messageLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // UpdatePromptForm
            // 
            this.AcceptButton        = this._downloadButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton        = this._cancelButton;
            this.ClientSize          = new System.Drawing.Size(613, 107);
            this.Controls.Add(this._contentPanel);
            this.Font            = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MaximumSize     = new System.Drawing.Size(736, 559);
            this.MinimizeBox     = false;
            this.Name            = "UpdatePromptForm";
            this.ShowInTaskbar   = false;
            this.StartPosition   = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text            = "Update available";
            this._contentPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }
    }
}
