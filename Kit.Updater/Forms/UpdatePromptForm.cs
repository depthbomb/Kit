namespace Kit.Updater.Forms;

internal enum UpdatePromptChoice
{
    Cancel,
    Download,
    LaunchCurrent,
    SkipVersion
}

internal sealed class UpdatePromptRequest
{
    public string WindowTitle             { get; set; } = string.Empty;
    public string Message                 { get; set; } = string.Empty;
    public string DownloadButtonText      { get; set; } = string.Empty;
    public string LaunchCurrentButtonText { get; set; } = string.Empty;
    public string SkipVersionButtonText   { get; set; } = string.Empty;
    public string CancelButtonText        { get; set; } = string.Empty;
    public bool AllowLaunchCurrent        { get; set; } = true;
    public bool AllowSkipVersion          { get; set; } = true;
}

internal sealed partial class UpdatePromptForm : Form
{
    private UpdatePromptForm()
    {
        InitializeComponent();
    }

    public UpdatePromptForm(UpdatePromptRequest request) : this()
    {
        ApplyRequest(request);
    }

    public UpdatePromptChoice Choice { get; private set; }

    private void ApplyRequest(UpdatePromptRequest request)
    {
        Text                      = request.WindowTitle;
        _messageLabel.Text        = request.Message;
        _downloadButton.Text      = request.DownloadButtonText;
        _launchCurrentButton.Text = request.LaunchCurrentButtonText;
        _skipVersionButton.Text   = request.SkipVersionButtonText;
        _cancelButton.Text        = request.CancelButtonText;
        _launchCurrentButton.Visible = request.AllowLaunchCurrent;
        _skipVersionButton.Visible   = request.AllowSkipVersion;
    }

    private void DownloadButtonClick(object sender, EventArgs e)
    {
        Choice       = UpdatePromptChoice.Download;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void LaunchCurrentButtonClick(object sender, EventArgs e)
    {
        Choice       = UpdatePromptChoice.LaunchCurrent;
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
