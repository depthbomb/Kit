using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Kit.Setup.Forms;

public partial class MainForm : Form
{
    private readonly SetupConfiguration _config;
    private readonly bool               _isUninstall;
    private readonly bool               _isDemoMode;
    private readonly List<Panel>        _panels = [];

    private int    _currentPanelIndex;
    private string _resolvedInstallDir = string.Empty;

    internal MainForm(SetupConfiguration config, bool isUninstall, bool isDemoMode)
    {
        _config      = config;
        _isUninstall = isUninstall;
        _isDemoMode  = isDemoMode;

        InitializeComponent();

        ConfigureWizard();
    }

    private void ConfigureWizard()
    {
        Text      = $"{_config.ApplicationName} Setup";
        BackColor = Color.FromArgb(248, 249, 250);

        c_ContentContainer.BackColor = Color.FromArgb(248, 249, 250);
        c_BannerTitle.Text           = _isUninstall ? $"Uninstall {_config.ApplicationName}" : $"Install {_config.ApplicationName}";
        c_BannerSubtitle.Text = _isUninstall
            ? $"Completely remove {_config.ApplicationName} and its components from your computer."
            : $"Configure and install {_config.ApplicationName} on your computer.";

        if (!string.IsNullOrWhiteSpace(_config.BannerImageBase64))
        {
            try
            {
                var bytes = Convert.FromBase64String(_config.BannerImageBase64);
                using (var ms = new MemoryStream(bytes))
                {
                    c_BannerImage.Image = Image.FromStream(ms);
                }
            }
            catch
            {
                /* Ignored */
            }
        }

        if (!string.IsNullOrWhiteSpace(_config.WindowIconBase64))
        {
            try
            {
                var bytes = Convert.FromBase64String(_config.WindowIconBase64);
                using (var ms = new MemoryStream(bytes))
                {
                    Icon = new Icon(ms);
                }
            }
            catch
            {
                /* Ignored */
            }
        }

        var baseLoc = _config.InstallLocation;
        if (string.Equals(baseLoc, "%APPDATA%", StringComparison.OrdinalIgnoreCase))
        {
            baseLoc = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (string.Equals(baseLoc, "%LOCALAPPDATA%", StringComparison.OrdinalIgnoreCase))
        {
            baseLoc = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        else
        {
            baseLoc = Environment.ExpandEnvironmentVariables(baseLoc);
        }

        _resolvedInstallDir  = Path.Combine(baseLoc, _config.OrganizationName, _config.ApplicationName);

        c_FolderTextBox.Text = _resolvedInstallDir;

        if (_isUninstall)
        {
            _panels.Add(c_UninstallConfirmPanel);
            _panels.Add(c_ProgressPanel);
            _panels.Add(c_FinishedPanel);

            c_UninstallConfirmText.Text = $"This wizard will completely uninstall {_config.ApplicationName} and all of its configurations from this computer.\r\n\r\nClick Uninstall to start the process.";

            c_FinishedHeader.Text       = "Uninstall Completed!";
            c_FinishedText.Text         = $"{_config.ApplicationName} was successfully removed from your computer.";
            c_LaunchAppCheckBox.Visible = false;
        }
        else
        {
            _panels.Add(c_WelcomePanel);
            _panels.Add(c_OptionsPanel);
            _panels.Add(c_ProgressPanel);
            _panels.Add(c_FinishedPanel);

            c_WelcomeText.Text = _config.WelcomeText.Replace("{ApplicationName}", _config.ApplicationName);
            if (_isDemoMode)
            {
                c_WelcomeDemoWarning.Visible = true;
                c_WelcomeDemoWarning.Text    = "DEBUG ALERT: Running in Mock Demo Mode (Installer is not stamped with actual app payload).";
            }

            c_PathCheckBox.Checked              = !string.IsNullOrWhiteSpace(_config.AddToPath);
            c_PathCheckBox.Visible              = !string.IsNullOrWhiteSpace(_config.AddToPath);
            c_DesktopShortcutCheckBox.Checked   = _config.CreateDesktopShortcut;
            c_StartMenuShortcutCheckBox.Checked = _config.CreateStartMenuShortcut;

            c_FinishedHeader.Text = "Installation Completed!";
            c_FinishedText.Text   = $"{_config.ApplicationName} has been successfully installed and configured on your computer.";

            if (!string.IsNullOrWhiteSpace(_config.LaunchExecutable))
            {
                c_LaunchAppCheckBox.Checked = true;
                c_LaunchAppCheckBox.Text    = $"Launch {_config.ApplicationName} now";
                c_LaunchAppCheckBox.Visible = true;
            }
            else
            {
                c_LaunchAppCheckBox.Visible = false;
            }
        }

        ShowPanel(_panels[0]);
        UpdateNavigationButtons();
    }

    private void ShowPanel(Panel targetPanel)
    {
        foreach (var p in _panels)
        {
            p.Visible = p == targetPanel;
        }
    }

    private void UpdateNavigationButtons()
    {
        var activePanel = _panels[_currentPanelIndex];

        if (_currentPanelIndex == 0)
        {
            c_BackButton.Enabled   = false;
            c_NextButton.Text      = _isUninstall ? "Uninstall" : "Next >";
            c_NextButton.Enabled   = true;
            c_CancelButton.Enabled = true;
        }
        else if (_currentPanelIndex == _panels.Count - 1)
        {
            c_BackButton.Enabled   = false;
            c_NextButton.Text      = "Finish";
            c_NextButton.Enabled   = true;
            c_CancelButton.Enabled = false;
        }
        else if (activePanel == c_ProgressPanel)
        {
            c_BackButton.Enabled   = false;
            c_NextButton.Enabled   = false;
            c_CancelButton.Enabled = false;
        }
        else
        {
            c_BackButton.Enabled   = true;
            c_NextButton.Text      = _currentPanelIndex == _panels.Count - 2 && !_isUninstall ? "Install" : "Next >";
            c_NextButton.Enabled   = true;
            c_CancelButton.Enabled = true;
        }
    }

    private void BackButton_Click(object sender, EventArgs e)
    {
        if (_currentPanelIndex > 0)
        {
            _currentPanelIndex--;
            ShowPanel(_panels[_currentPanelIndex]);
            UpdateNavigationButtons();
        }
    }

    private void NextButton_Click(object sender, EventArgs e)
    {
        if (_currentPanelIndex == _panels.Count - 1)
        {
            if (!_isUninstall && c_LaunchAppCheckBox.Checked && !string.IsNullOrWhiteSpace(_config.LaunchExecutable))
            {
                try
                {
                    var exePath = Path.Combine(_resolvedInstallDir, _config.LaunchExecutable);
                    if (File.Exists(exePath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName         = exePath,
                            Arguments        = _config.LaunchArguments,
                            WorkingDirectory = _resolvedInstallDir
                        });
                    }
                }
                catch
                {
                    /* Ignored */
                }
            }

            Close();
            return;
        }

        var nextPanel = _panels[_currentPanelIndex + 1];
        if (nextPanel == c_ProgressPanel)
        {
            StartWorkflow();
        }
        else
        {
            _currentPanelIndex++;

            ShowPanel(_panels[_currentPanelIndex]);
            UpdateNavigationButtons();
        }
    }

    private void CancelButton_Click(object sender, EventArgs e)
    {
        var activePanel = _panels[_currentPanelIndex];
        if (activePanel == c_ProgressPanel) return;

        var result = MessageBox.Show(
            this,
            "Are you sure you want to exit the setup?",
            "Exit Setup",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            Close();
        }
    }

    private void FolderBrowseButton_Click(object sender, EventArgs e)
    {
        using (var fbd = new FolderBrowserDialog())
        {
            fbd.Description  = "Select target folder to install to:";
            fbd.SelectedPath = c_FolderTextBox.Text;

            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                c_FolderTextBox.Text = fbd.SelectedPath;
            }
        }
    }

    private async void StartWorkflow()
    {
        _currentPanelIndex = _panels.IndexOf(c_ProgressPanel);

        ShowPanel(c_ProgressPanel);
        UpdateNavigationButtons();

        c_ProgressBar.Style = ProgressBarStyle.Marquee;
        c_ProgressStepsList.Items.Clear();

        IProgress<string> progress = new Progress<string>(step =>
        {
            c_ProgressStatusLabel.Text = step;
            c_ProgressStepsList.Items.Add($"• {step}");
            c_ProgressStepsList.SelectedIndex = c_ProgressStepsList.Items.Count - 1;
        });

        _resolvedInstallDir = c_FolderTextBox.Text.Trim();

        try
        {
            if (_isUninstall)
            {
                c_ProgressStatusLabel.Text = "Removing application...";

                if (!string.IsNullOrWhiteSpace(_config.ProcessName))
                {
                    progress.Report("Checking for running instances...");
                    if (!EnsureProcessClosed(_config.ProcessName, _config.ApplicationName))
                    {
                        throw new OperationCanceledException("Uninstallation canceled: application is running.");
                    }
                }

                await Task.Run(() => SetupWorkflow.UninstallSync(_config, _resolvedInstallDir, progress));
            }
            else
            {
                c_ProgressStatusLabel.Text = "Installing application...";

                if (!string.IsNullOrWhiteSpace(_config.ProcessName))
                {
                    progress.Report("Checking for running instances...");
                    if (!EnsureProcessClosed(_config.ProcessName, _config.ApplicationName))
                    {
                        throw new OperationCanceledException("Installation canceled: application is running.");
                    }
                }

                var addToPath         = c_PathCheckBox.Checked;
                var desktopShortcut   = c_DesktopShortcutCheckBox.Checked;
                var startMenuShortcut = c_StartMenuShortcutCheckBox.Checked;

                await Task.Run(() => SetupWorkflow.InstallSync(
                    _config,
                    _resolvedInstallDir,
                    addToPath,
                    desktopShortcut,
                    startMenuShortcut,
                    progress));
            }

            _currentPanelIndex = _panels.IndexOf(c_FinishedPanel);

            ShowPanel(c_FinishedPanel);
            UpdateNavigationButtons();
        }
        catch (OperationCanceledException)
        {
            // Canceled by user
            _currentPanelIndex = _isUninstall ? 0 : 1;

            ShowPanel(_panels[_currentPanelIndex]);
            UpdateNavigationButtons();
        }
        catch (Exception ex)
        {
            c_ProgressStatusLabel.Text = "Error occurred during operations.";
            MessageBox.Show(this, "Setup failed:\r\n" + ex.Message, "Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            _currentPanelIndex = _isUninstall ? 0 : 1;
            ShowPanel(_panels[_currentPanelIndex]);
            UpdateNavigationButtons();
        }
    }

    private bool EnsureProcessClosed(string processName, string appName)
    {
        while (Process.GetProcessesByName(processName).Length > 0)
        {
            var result = MessageBox.Show(
                this,
                $"{appName} is currently running on your computer. Please save your work, close the application, and then click Retry to continue.",
                $"{appName} is Running",
                MessageBoxButtons.RetryCancel,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Cancel)
            {
                return false;
            }
        }

        return true;
    }
}
