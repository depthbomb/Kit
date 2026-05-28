namespace Kit.Setup.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    // Banner
    private System.Windows.Forms.Panel      c_BannerPanel;
    private System.Windows.Forms.Label      c_BannerTitle;
    private System.Windows.Forms.Label      c_BannerSubtitle;
    private System.Windows.Forms.PictureBox c_BannerImage;

    // Bottom Navigation
    private System.Windows.Forms.Panel  c_BottomPanel;
    private System.Windows.Forms.Button c_BackButton;
    private System.Windows.Forms.Button c_NextButton;
    private System.Windows.Forms.Button c_CancelButton;

    // Main Content Container
    private System.Windows.Forms.Panel c_ContentContainer;

    // Wizard Panels
    private System.Windows.Forms.Panel c_WelcomePanel;
    private System.Windows.Forms.Panel c_OptionsPanel;
    private System.Windows.Forms.Panel c_ProgressPanel;
    private System.Windows.Forms.Panel c_FinishedPanel;
    private System.Windows.Forms.Panel c_UninstallConfirmPanel;

    // Welcome Panel Controls
    private System.Windows.Forms.Label c_WelcomeText;
    private System.Windows.Forms.Label c_WelcomeDemoWarning;

    // Options Panel Controls
    private System.Windows.Forms.Label    c_FolderLabel;
    private System.Windows.Forms.TextBox  c_FolderTextBox;
    private System.Windows.Forms.Button   c_FolderBrowseButton;
    private System.Windows.Forms.CheckBox c_PathCheckBox;
    private System.Windows.Forms.CheckBox c_DesktopShortcutCheckBox;
    private System.Windows.Forms.CheckBox c_StartMenuShortcutCheckBox;

    // Progress Panel Controls
    private System.Windows.Forms.ProgressBar c_ProgressBar;
    private System.Windows.Forms.Label       c_ProgressStatusLabel;
    private System.Windows.Forms.ListBox     c_ProgressStepsList;

    // Finished Panel Controls
    private System.Windows.Forms.Label    c_FinishedHeader;
    private System.Windows.Forms.Label    c_FinishedText;
    private System.Windows.Forms.CheckBox c_LaunchAppCheckBox;

    // Uninstall Confirm Panel Controls
    private System.Windows.Forms.Label c_UninstallConfirmText;

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
        this.c_BannerPanel               = new System.Windows.Forms.Panel();
        this.c_BannerSubtitle            = new System.Windows.Forms.Label();
        this.c_BannerTitle               = new System.Windows.Forms.Label();
        this.c_BannerImage               = new System.Windows.Forms.PictureBox();
        this.c_BottomPanel               = new System.Windows.Forms.Panel();
        this.c_BackButton                = new System.Windows.Forms.Button();
        this.c_NextButton                = new System.Windows.Forms.Button();
        this.c_CancelButton              = new System.Windows.Forms.Button();
        this.c_ContentContainer          = new System.Windows.Forms.Panel();
        this.c_WelcomePanel              = new System.Windows.Forms.Panel();
        this.c_WelcomeDemoWarning        = new System.Windows.Forms.Label();
        this.c_WelcomeText               = new System.Windows.Forms.Label();
        this.c_OptionsPanel              = new System.Windows.Forms.Panel();
        this.c_StartMenuShortcutCheckBox = new System.Windows.Forms.CheckBox();
        this.c_DesktopShortcutCheckBox   = new System.Windows.Forms.CheckBox();
        this.c_PathCheckBox              = new System.Windows.Forms.CheckBox();
        this.c_FolderBrowseButton        = new System.Windows.Forms.Button();
        this.c_FolderTextBox             = new System.Windows.Forms.TextBox();
        this.c_FolderLabel               = new System.Windows.Forms.Label();
        this.c_ProgressPanel             = new System.Windows.Forms.Panel();
        this.c_ProgressStepsList         = new System.Windows.Forms.ListBox();
        this.c_ProgressStatusLabel       = new System.Windows.Forms.Label();
        this.c_ProgressBar               = new System.Windows.Forms.ProgressBar();
        this.c_FinishedPanel             = new System.Windows.Forms.Panel();
        this.c_LaunchAppCheckBox         = new System.Windows.Forms.CheckBox();
        this.c_FinishedText              = new System.Windows.Forms.Label();
        this.c_FinishedHeader            = new System.Windows.Forms.Label();
        this.c_UninstallConfirmPanel     = new System.Windows.Forms.Panel();
        this.c_UninstallConfirmText      = new System.Windows.Forms.Label();
        this.c_BannerPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.c_BannerImage)).BeginInit();
        this.c_BottomPanel.SuspendLayout();
        this.c_ContentContainer.SuspendLayout();
        this.c_WelcomePanel.SuspendLayout();
        this.c_OptionsPanel.SuspendLayout();
        this.c_ProgressPanel.SuspendLayout();
        this.c_FinishedPanel.SuspendLayout();
        this.c_UninstallConfirmPanel.SuspendLayout();
        this.SuspendLayout();
        // 
        // c_BannerPanel
        // 
        this.c_BannerPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(49)))), ((int)(((byte)(76)))));
        this.c_BannerPanel.Controls.Add(this.c_BannerSubtitle);
        this.c_BannerPanel.Controls.Add(this.c_BannerTitle);
        this.c_BannerPanel.Controls.Add(this.c_BannerImage);
        this.c_BannerPanel.Dock     = System.Windows.Forms.DockStyle.Top;
        this.c_BannerPanel.Location = new System.Drawing.Point(0, 0);
        this.c_BannerPanel.Name     = "c_BannerPanel";
        this.c_BannerPanel.Size     = new System.Drawing.Size(594, 85);
        this.c_BannerPanel.TabIndex = 0;
        // 
        // c_BannerSubtitle
        // 
        this.c_BannerSubtitle.AutoSize  = true;
        this.c_BannerSubtitle.BackColor = System.Drawing.Color.Transparent;
        this.c_BannerSubtitle.Font      = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
        this.c_BannerSubtitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(210)))), ((int)(((byte)(220)))), ((int)(((byte)(235)))));
        this.c_BannerSubtitle.Location  = new System.Drawing.Point(17, 49);
        this.c_BannerSubtitle.Name      = "c_BannerSubtitle";
        this.c_BannerSubtitle.Size      = new System.Drawing.Size(285, 15);
        this.c_BannerSubtitle.TabIndex  = 1;
        this.c_BannerSubtitle.Text      = "Please wait while the installation is being configured.";
        // 
        // c_BannerTitle
        // 
        this.c_BannerTitle.AutoSize  = true;
        this.c_BannerTitle.BackColor = System.Drawing.Color.Transparent;
        this.c_BannerTitle.Font      = new System.Drawing.Font("Segoe UI Semibold", 15F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
        this.c_BannerTitle.ForeColor = System.Drawing.Color.White;
        this.c_BannerTitle.Location  = new System.Drawing.Point(15, 18);
        this.c_BannerTitle.Name      = "c_BannerTitle";
        this.c_BannerTitle.Size      = new System.Drawing.Size(174, 28);
        this.c_BannerTitle.TabIndex  = 0;
        this.c_BannerTitle.Text      = "Install Application";
        // 
        // c_BannerImage
        // 
        this.c_BannerImage.Dock     = System.Windows.Forms.DockStyle.Fill;
        this.c_BannerImage.Location = new System.Drawing.Point(0, 0);
        this.c_BannerImage.Name     = "c_BannerImage";
        this.c_BannerImage.Size     = new System.Drawing.Size(594, 85);
        this.c_BannerImage.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
        this.c_BannerImage.TabIndex = 2;
        this.c_BannerImage.TabStop  = false;
        // 
        // c_BottomPanel
        // 
        this.c_BottomPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
        this.c_BottomPanel.Controls.Add(this.c_BackButton);
        this.c_BottomPanel.Controls.Add(this.c_NextButton);
        this.c_BottomPanel.Controls.Add(this.c_CancelButton);
        this.c_BottomPanel.Dock     = System.Windows.Forms.DockStyle.Bottom;
        this.c_BottomPanel.Location = new System.Drawing.Point(0, 371);
        this.c_BottomPanel.Name     = "c_BottomPanel";
        this.c_BottomPanel.Size     = new System.Drawing.Size(594, 50);
        this.c_BottomPanel.TabIndex = 1;
        // 
        // c_BackButton
        // 
        this.c_BackButton.Location                =  new System.Drawing.Point(330, 12);
        this.c_BackButton.Name                    =  "c_BackButton";
        this.c_BackButton.Size                    =  new System.Drawing.Size(80, 26);
        this.c_BackButton.TabIndex                =  0;
        this.c_BackButton.Text                    =  "< Back";
        this.c_BackButton.UseVisualStyleBackColor =  true;
        this.c_BackButton.Click                   += new System.EventHandler(this.BackButton_Click);
        // 
        // c_NextButton
        // 
        this.c_NextButton.Location                =  new System.Drawing.Point(416, 12);
        this.c_NextButton.Name                    =  "c_NextButton";
        this.c_NextButton.Size                    =  new System.Drawing.Size(80, 26);
        this.c_NextButton.TabIndex                =  1;
        this.c_NextButton.Text                    =  "Next >";
        this.c_NextButton.UseVisualStyleBackColor =  true;
        this.c_NextButton.Click                   += new System.EventHandler(this.NextButton_Click);
        // 
        // c_CancelButton
        // 
        this.c_CancelButton.Location                =  new System.Drawing.Point(502, 12);
        this.c_CancelButton.Name                    =  "c_CancelButton";
        this.c_CancelButton.Size                    =  new System.Drawing.Size(80, 26);
        this.c_CancelButton.TabIndex                =  2;
        this.c_CancelButton.Text                    =  "Cancel";
        this.c_CancelButton.UseVisualStyleBackColor =  true;
        this.c_CancelButton.Click                   += new System.EventHandler(this.CancelButton_Click);
        // 
        // c_ContentContainer
        // 
        this.c_ContentContainer.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
        this.c_ContentContainer.Controls.Add(this.c_WelcomePanel);
        this.c_ContentContainer.Controls.Add(this.c_OptionsPanel);
        this.c_ContentContainer.Controls.Add(this.c_ProgressPanel);
        this.c_ContentContainer.Controls.Add(this.c_FinishedPanel);
        this.c_ContentContainer.Controls.Add(this.c_UninstallConfirmPanel);
        this.c_ContentContainer.Dock     = System.Windows.Forms.DockStyle.Fill;
        this.c_ContentContainer.Location = new System.Drawing.Point(0, 85);
        this.c_ContentContainer.Name     = "c_ContentContainer";
        this.c_ContentContainer.Size     = new System.Drawing.Size(594, 286);
        this.c_ContentContainer.TabIndex = 2;
        // 
        // c_WelcomePanel
        // 
        this.c_WelcomePanel.Controls.Add(this.c_WelcomeDemoWarning);
        this.c_WelcomePanel.Controls.Add(this.c_WelcomeText);
        this.c_WelcomePanel.Dock     = System.Windows.Forms.DockStyle.Fill;
        this.c_WelcomePanel.Location = new System.Drawing.Point(0, 0);
        this.c_WelcomePanel.Name     = "c_WelcomePanel";
        this.c_WelcomePanel.Size     = new System.Drawing.Size(594, 286);
        this.c_WelcomePanel.TabIndex = 0;
        // 
        // c_WelcomeDemoWarning
        // 
        this.c_WelcomeDemoWarning.Font      = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
        this.c_WelcomeDemoWarning.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(50)))), ((int)(((byte)(50)))));
        this.c_WelcomeDemoWarning.Location  = new System.Drawing.Point(20, 190);
        this.c_WelcomeDemoWarning.Name      = "c_WelcomeDemoWarning";
        this.c_WelcomeDemoWarning.Size      = new System.Drawing.Size(540, 60);
        this.c_WelcomeDemoWarning.TabIndex  = 1;
        this.c_WelcomeDemoWarning.Text      = "Running in Demo Mode!";
        this.c_WelcomeDemoWarning.Visible   = false;
        // 
        // c_WelcomeText
        // 
        this.c_WelcomeText.Font      = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
        this.c_WelcomeText.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
        this.c_WelcomeText.Location  = new System.Drawing.Point(20, 20);
        this.c_WelcomeText.Name      = "c_WelcomeText";
        this.c_WelcomeText.Size      = new System.Drawing.Size(540, 160);
        this.c_WelcomeText.TabIndex  = 0;
        this.c_WelcomeText.Text      = "Welcome to the application setup.";
        // 
        // c_OptionsPanel
        // 
        this.c_OptionsPanel.Controls.Add(this.c_StartMenuShortcutCheckBox);
        this.c_OptionsPanel.Controls.Add(this.c_DesktopShortcutCheckBox);
        this.c_OptionsPanel.Controls.Add(this.c_PathCheckBox);
        this.c_OptionsPanel.Controls.Add(this.c_FolderBrowseButton);
        this.c_OptionsPanel.Controls.Add(this.c_FolderTextBox);
        this.c_OptionsPanel.Controls.Add(this.c_FolderLabel);
        this.c_OptionsPanel.Dock     = System.Windows.Forms.DockStyle.Fill;
        this.c_OptionsPanel.Location = new System.Drawing.Point(0, 0);
        this.c_OptionsPanel.Name     = "c_OptionsPanel";
        this.c_OptionsPanel.Size     = new System.Drawing.Size(594, 286);
        this.c_OptionsPanel.TabIndex = 1;
        // 
        // c_StartMenuShortcutCheckBox
        // 
        this.c_StartMenuShortcutCheckBox.AutoSize                = true;
        this.c_StartMenuShortcutCheckBox.Location                = new System.Drawing.Point(23, 150);
        this.c_StartMenuShortcutCheckBox.Name                    = "c_StartMenuShortcutCheckBox";
        this.c_StartMenuShortcutCheckBox.Size                    = new System.Drawing.Size(169, 19);
        this.c_StartMenuShortcutCheckBox.TabIndex                = 5;
        this.c_StartMenuShortcutCheckBox.Text                    = "Create Start Menu Shortcut";
        this.c_StartMenuShortcutCheckBox.UseVisualStyleBackColor = true;
        // 
        // c_DesktopShortcutCheckBox
        // 
        this.c_DesktopShortcutCheckBox.AutoSize                = true;
        this.c_DesktopShortcutCheckBox.Location                = new System.Drawing.Point(23, 120);
        this.c_DesktopShortcutCheckBox.Name                    = "c_DesktopShortcutCheckBox";
        this.c_DesktopShortcutCheckBox.Size                    = new System.Drawing.Size(154, 19);
        this.c_DesktopShortcutCheckBox.TabIndex                = 4;
        this.c_DesktopShortcutCheckBox.Text                    = "Create Desktop Shortcut";
        this.c_DesktopShortcutCheckBox.UseVisualStyleBackColor = true;
        // 
        // c_PathCheckBox
        // 
        this.c_PathCheckBox.AutoSize                = true;
        this.c_PathCheckBox.Location                = new System.Drawing.Point(23, 90);
        this.c_PathCheckBox.Name                    = "c_PathCheckBox";
        this.c_PathCheckBox.Size                    = new System.Drawing.Size(331, 19);
        this.c_PathCheckBox.TabIndex                = 3;
        this.c_PathCheckBox.Text                    = "Add CLI tools folder to PATH (recommended for CLI apps)";
        this.c_PathCheckBox.UseVisualStyleBackColor = true;
        // 
        // c_FolderBrowseButton
        // 
        this.c_FolderBrowseButton.Location                =  new System.Drawing.Point(466, 39);
        this.c_FolderBrowseButton.Name                    =  "c_FolderBrowseButton";
        this.c_FolderBrowseButton.Size                    =  new System.Drawing.Size(94, 25);
        this.c_FolderBrowseButton.TabIndex                =  2;
        this.c_FolderBrowseButton.Text                    =  "Browse...";
        this.c_FolderBrowseButton.UseVisualStyleBackColor =  true;
        this.c_FolderBrowseButton.Click                   += new System.EventHandler(this.FolderBrowseButton_Click);
        // 
        // c_FolderTextBox
        // 
        this.c_FolderTextBox.Location = new System.Drawing.Point(23, 40);
        this.c_FolderTextBox.Name     = "c_FolderTextBox";
        this.c_FolderTextBox.Size     = new System.Drawing.Size(437, 23);
        this.c_FolderTextBox.TabIndex = 1;
        // 
        // c_FolderLabel
        // 
        this.c_FolderLabel.AutoSize = true;
        this.c_FolderLabel.Location = new System.Drawing.Point(20, 20);
        this.c_FolderLabel.Name     = "c_FolderLabel";
        this.c_FolderLabel.Size     = new System.Drawing.Size(104, 15);
        this.c_FolderLabel.TabIndex = 0;
        this.c_FolderLabel.Text     = "Install Folder Path:";
        // 
        // c_ProgressPanel
        // 
        this.c_ProgressPanel.Controls.Add(this.c_ProgressStepsList);
        this.c_ProgressPanel.Controls.Add(this.c_ProgressStatusLabel);
        this.c_ProgressPanel.Controls.Add(this.c_ProgressBar);
        this.c_ProgressPanel.Dock     = System.Windows.Forms.DockStyle.Fill;
        this.c_ProgressPanel.Location = new System.Drawing.Point(0, 0);
        this.c_ProgressPanel.Name     = "c_ProgressPanel";
        this.c_ProgressPanel.Size     = new System.Drawing.Size(594, 286);
        this.c_ProgressPanel.TabIndex = 2;
        // 
        // c_ProgressStepsList
        // 
        this.c_ProgressStepsList.BackColor         = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
        this.c_ProgressStepsList.BorderStyle       = System.Windows.Forms.BorderStyle.None;
        this.c_ProgressStepsList.FormattingEnabled = true;
        this.c_ProgressStepsList.ItemHeight        = 15;
        this.c_ProgressStepsList.Location          = new System.Drawing.Point(23, 85);
        this.c_ProgressStepsList.Name              = "c_ProgressStepsList";
        this.c_ProgressStepsList.SelectionMode     = System.Windows.Forms.SelectionMode.None;
        this.c_ProgressStepsList.Size              = new System.Drawing.Size(537, 165);
        this.c_ProgressStepsList.TabIndex          = 2;
        // 
        // c_ProgressStatusLabel
        // 
        this.c_ProgressStatusLabel.AutoSize = true;
        this.c_ProgressStatusLabel.Location = new System.Drawing.Point(20, 20);
        this.c_ProgressStatusLabel.Name     = "c_ProgressStatusLabel";
        this.c_ProgressStatusLabel.Size     = new System.Drawing.Size(112, 15);
        this.c_ProgressStatusLabel.TabIndex = 1;
        this.c_ProgressStatusLabel.Text     = "Configuring setup...";
        // 
        // c_ProgressBar
        // 
        this.c_ProgressBar.Location              = new System.Drawing.Point(23, 40);
        this.c_ProgressBar.MarqueeAnimationSpeed = 30;
        this.c_ProgressBar.Name                  = "c_ProgressBar";
        this.c_ProgressBar.Size                  = new System.Drawing.Size(537, 23);
        this.c_ProgressBar.Style                 = System.Windows.Forms.ProgressBarStyle.Marquee;
        this.c_ProgressBar.TabIndex              = 0;
        // 
        // c_FinishedPanel
        // 
        this.c_FinishedPanel.Controls.Add(this.c_LaunchAppCheckBox);
        this.c_FinishedPanel.Controls.Add(this.c_FinishedText);
        this.c_FinishedPanel.Controls.Add(this.c_FinishedHeader);
        this.c_FinishedPanel.Dock     = System.Windows.Forms.DockStyle.Fill;
        this.c_FinishedPanel.Location = new System.Drawing.Point(0, 0);
        this.c_FinishedPanel.Name     = "c_FinishedPanel";
        this.c_FinishedPanel.Size     = new System.Drawing.Size(594, 286);
        this.c_FinishedPanel.TabIndex = 3;
        // 
        // c_LaunchAppCheckBox
        // 
        this.c_LaunchAppCheckBox.AutoSize                = true;
        this.c_LaunchAppCheckBox.Location                = new System.Drawing.Point(25, 170);
        this.c_LaunchAppCheckBox.Name                    = "c_LaunchAppCheckBox";
        this.c_LaunchAppCheckBox.Size                    = new System.Drawing.Size(114, 19);
        this.c_LaunchAppCheckBox.TabIndex                = 2;
        this.c_LaunchAppCheckBox.Text                    = "Launch app now";
        this.c_LaunchAppCheckBox.UseVisualStyleBackColor = true;
        // 
        // c_FinishedText
        // 
        this.c_FinishedText.Location = new System.Drawing.Point(21, 55);
        this.c_FinishedText.Name     = "c_FinishedText";
        this.c_FinishedText.Size     = new System.Drawing.Size(539, 100);
        this.c_FinishedText.TabIndex = 1;
        this.c_FinishedText.Text     = "The application has been successfully configured on your machine.";
        // 
        // c_FinishedHeader
        // 
        this.c_FinishedHeader.AutoSize = true;
        this.c_FinishedHeader.Font     = new System.Drawing.Font("Segoe UI Semibold", 13F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
        this.c_FinishedHeader.Location = new System.Drawing.Point(20, 20);
        this.c_FinishedHeader.Name     = "c_FinishedHeader";
        this.c_FinishedHeader.Size     = new System.Drawing.Size(207, 25);
        this.c_FinishedHeader.TabIndex = 0;
        this.c_FinishedHeader.Text     = "Installation Completed!";
        // 
        // c_UninstallConfirmPanel
        // 
        this.c_UninstallConfirmPanel.Controls.Add(this.c_UninstallConfirmText);
        this.c_UninstallConfirmPanel.Dock     = System.Windows.Forms.DockStyle.Fill;
        this.c_UninstallConfirmPanel.Location = new System.Drawing.Point(0, 0);
        this.c_UninstallConfirmPanel.Name     = "c_UninstallConfirmPanel";
        this.c_UninstallConfirmPanel.Size     = new System.Drawing.Size(594, 286);
        this.c_UninstallConfirmPanel.TabIndex = 4;
        // 
        // c_UninstallConfirmText
        // 
        this.c_UninstallConfirmText.Font     = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
        this.c_UninstallConfirmText.Location = new System.Drawing.Point(20, 20);
        this.c_UninstallConfirmText.Name     = "c_UninstallConfirmText";
        this.c_UninstallConfirmText.Size     = new System.Drawing.Size(540, 200);
        this.c_UninstallConfirmText.TabIndex = 0;
        this.c_UninstallConfirmText.Text     = "Are you sure you want to completely uninstall this application and all of its com" + "ponents?";
        // 
        // MainForm
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize          = new System.Drawing.Size(594, 421);
        this.Controls.Add(this.c_ContentContainer);
        this.Controls.Add(this.c_BottomPanel);
        this.Controls.Add(this.c_BannerPanel);
        this.Font            = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox     = false;
        this.MinimizeBox     = false;
        this.MinimumSize     = new System.Drawing.Size(600, 450);
        this.Name            = "MainForm";
        this.StartPosition   = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text            = "Setup";
        this.c_BannerPanel.ResumeLayout(false);
        this.c_BannerPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this.c_BannerImage)).EndInit();
        this.c_BottomPanel.ResumeLayout(false);
        this.c_ContentContainer.ResumeLayout(false);
        this.c_WelcomePanel.ResumeLayout(false);
        this.c_OptionsPanel.ResumeLayout(false);
        this.c_OptionsPanel.PerformLayout();
        this.c_ProgressPanel.ResumeLayout(false);
        this.c_ProgressPanel.PerformLayout();
        this.c_FinishedPanel.ResumeLayout(false);
        this.c_FinishedPanel.PerformLayout();
        this.c_UninstallConfirmPanel.ResumeLayout(false);
        this.ResumeLayout(false);
    }
    #endregion
}
