namespace Kit.Updater.Forms;

internal sealed partial class UpdatePromptForm : Form
{
    public UpdatePromptChoice Choice { get; private set; }

    private UpdatePromptForm()
    {
        InitializeComponent();
    }

    public UpdatePromptForm(UpdatePromptRequest request) : this()
    {
        ApplyRequest(request);
    }

    private void ApplyRequest(UpdatePromptRequest request)
    {
        Text                          = request.WindowTitle;
        _messageLabel.Text            = request.Message;
        _downloadButton.Text          = request.DownloadButtonText;
        _skipForSessionButton.Text    = request.SkipForSessionButtonText;
        _skipVersionButton.Text       = request.SkipVersionButtonText;
        _cancelButton.Text            = request.CancelButtonText;
        _skipForSessionButton.Visible = request.AllowSkipForSession;
        _skipVersionButton.Visible    = request.AllowSkipVersion;
    }

    private void DownloadButtonClick(object sender, EventArgs e)
    {
        Choice       = UpdatePromptChoice.Download;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void SkipForSessionButtonClick(object sender, EventArgs e)
    {
        Choice       = UpdatePromptChoice.SkipForSession;
        DialogResult = DialogResult.OK;

        Close();
    }

    private void SkipVersionButtonClick(object sender, EventArgs e)
    {
        Choice       = UpdatePromptChoice.SkipVersion;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CancelButtonClick(object sender, EventArgs e)
    {
        Choice       = UpdatePromptChoice.Cancel;
        DialogResult = DialogResult.Cancel;

        Close();
    }
}

internal enum UpdatePromptChoice
{
    Cancel,
    Download,
    SkipForSession,
    SkipVersion
}

internal sealed class UpdatePromptRequest
{
    public string WindowTitle              { get; set; } = string.Empty;
    public string Message                  { get; set; } = string.Empty;
    public string DownloadButtonText       { get; set; } = string.Empty;
    public string SkipForSessionButtonText { get; set; } = string.Empty;
    public string SkipVersionButtonText    { get; set; } = string.Empty;
    public string CancelButtonText         { get; set; } = string.Empty;
    public bool   AllowSkipForSession      { get; set; } = true;
    public bool   AllowSkipVersion         { get; set; } = true;
}
