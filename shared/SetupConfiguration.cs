namespace Shared;

internal sealed class SetupConfiguration
{
    public string ApplicationName { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    
    // General location to install to: "%APPDATA%" or "%LOCALAPPDATA%" (or custom)
    public string InstallLocation { get; set; } = "%LOCALAPPDATA%";
    
    // Process name to check for running instances before installing/uninstalling
    public string ProcessName { get; set; } = string.Empty;
    
    // Relative path inside the extraction folder to add to PATH, e.g., "bin" or "" for root. Leave empty to skip.
    public string AddToPath { get; set; } = string.Empty;
    
    public string LaunchExecutable { get; set; } = string.Empty;
    public string LaunchArguments { get; set; } = string.Empty;
    
    public bool CreateDesktopShortcut { get; set; } = true;
    public bool CreateStartMenuShortcut { get; set; } = true;
    
    // Pre-install and post-install commands to run
    public string PreInstallCommand { get; set; } = string.Empty;
    public string PreInstallArguments { get; set; } = string.Empty;
    
    public string PostInstallCommand { get; set; } = string.Empty;
    public string PostInstallArguments { get; set; } = string.Empty;
    
    public string WindowIconBase64 { get; set; } = string.Empty;
    public string BannerImageBase64 { get; set; } = string.Empty;
    
    public string WelcomeText { get; set; } = "This wizard will guide you through the installation of {ApplicationName}.";
    
    // The zipped application payload (base64 serialized in JSON)
    public string PackageZipBase64 { get; set; } = string.Empty;
}
