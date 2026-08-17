# Kit

A Windows bootstrapper and release tooling set for self-updating desktop apps.

It consists of:

- `Kit.Updater`: a .NET Framework 4.8 WinForms bootstrapper that reads a stamped configuration payload from its own executable, checks for updates, installs app payloads, and launches the app.
- `Kit.Cli`: a CLI used to stamp the updater binary and generate release artifacts during packaging or CI/CD.

## What The Project Currently Supports

- Stamping a blank updater executable with app metadata and embedded branding assets
- Checking for updates from either GitHub Releases or a hosted JSON manifest
- Optional, required, and minimum-version-required update policies
- Fresh install when no local `app-*` folder exists and `allowFreshInstall` is enabled
- ZIP extraction built in, with optional `.7z` support when `bin\7za.exe` is shipped next to the updater
- SHA-256 verification of downloaded payloads
- Optional post-install command execution
- Optional NTFS compression of extracted files
- Optional installation of required .NET runtimes
- Optional installation of Windows App Runtime
- Optional installation of Microsoft Edge WebView2 Runtime
- Updater-refresh releases via an external installer plus `release-manifest.json`

## CLI Commands

The CLI exposes four commands:

```text
kit stamp --input <blank-bootstrapper.exe> --config <stamp-config.json> [--output <stamped-bootstrapper.exe>] [--version <release-version>]
kit inspect --input <stamped-bootstrapper.exe>
kit manifest --version <release-version> --updater <stamped-updater.exe> --package <app-package.zip> [--installer <updater-refresh-installer.exe>] [--output <output-directory>] [--updater-update-required <true|false>]
kit release --app-dir <app-dir-path> --config <stamp-config.json> --updater <blank-updater.exe> [--version <release-version>] [--output-dir <output-dir-path>] [--package-name <app-package.zip>] [--updater-update-required <true|false>] [--installer-command <command>] [--installer-args <args>] [--installer-path <installer-path>]
```

The CLI also reads `.kitrc`, `.kitrc.yml`, or `.kitrc.yaml` from the current directory or any parent directory. Values in that file act as defaults and can still be overridden on the command line.

See `.kitrc.sample.yaml` for a ready-made example.

### `stamp`

Writes the stamped updater executable. If `--output` is omitted, the input file is overwritten.
When the updater will be shipped in an updater-refresh installer, pass `--version` with the same version used by `manifest`; the CLI rejects updater-refresh manifests whose updater was not stamped for that release.

```powershell
.\kit.exe stamp `
  --input .\Kit.Updater\bin\Release\net48\Kit.Updater.exe `
  --config .\stamp-config.sample.json `
  --output .\out\MyApp-Updater.exe
```

### `inspect`

Reads the stamped updater payload and prints the embedded configuration as formatted JSON.

```powershell
.\kit.exe inspect `
  --input .\out\MyApp-Updater.exe
```

### `manifest`

Builds `release-manifest.json` in the target output directory. The manifest always contains the application package. `--installer` is only required when `--updater-update-required true`.

```powershell
.\kit.exe manifest `
  --version 1.1.0 `
  --updater .\out\MyApp-Updater.exe `
  --package .\artifacts\MyApp-1.1.0.zip `
  --output .\artifacts `
  --updater-update-required false
```

### `release`

Runs the end-to-end release flow:

1. Reads the stamp config.
2. Auto-detects the app version from `launchExecutable` unless `--version` is supplied.
3. Zips `--app-dir`.
4. Stamps the updater in place.
5. Optionally runs an installer build command.
6. Writes `release-manifest.json`.

Example:

```powershell
.\kit.exe release `
  --app-dir .\publish\MyApp `
  --config .\stamp-config.sample.json `
  --updater .\Kit.Updater\bin\Release\net48\Kit.Updater.exe `
  --output-dir .\artifacts `
  --package-name MyApp.zip `
  --installer-command "iscc.exe" `
  --installer-args ".\installer.iss /DAppVersion={Version} /DOutputDir={OutputDir}" `
  --installer-path "{OutputDir}\MyApp-Setup.exe"
```

With a populated `.kitrc`, the same release can usually shrink to:

```powershell
.\kit.exe release
```

## Stamp Configuration

Use **stamp-config.sample.json** as the reference template.

Current top-level fields:

- `applicationName` required
- `initialVersion` required
- `launchExecutable` required
- `launchArguments` optional
- `bannerImagePath` optional
- `windowIconPath` optional, must point to an `.ico`
- `requiredAppRuntimeVersion` optional
- `requireWebView2Runtime` optional
- `appearance` optional
- `text` optional
- `installation` optional
- `updatePolicy` optional
- `requiredRuntimes` optional
- `updateSource` required

Example:

```json
{
  "applicationName": "My App",
  "initialVersion": "1.0.0",
  "launchExecutable": "MyApp.exe",
  "launchArguments": "",
  "bannerImagePath": "assets/banner.png",
  "windowIconPath": "assets/app.ico",
  "requiredAppRuntimeVersion": "",
  "requireWebView2Runtime": false,
  "appearance": {
    "useDarkMode": true,
    "useDarkTitleBar": true,
    "backgroundColor": "#202020",
    "foregroundColor": "#f5f5f5",
    "secondaryTextColor": "#d2d2d2",
    "bannerBackgroundColor": "#161616"
  },
  "text": {
    "windowTitle": "{ApplicationName} Launcher",
    "title": "{ApplicationName}"
  },
  "installation": {
    "requireIntegrityVerification": true,
    "processName": "MyApp",
    "keepLastVersions": 3,
    "extractionLayout": "auto",
    "postInstallCommand": "postinstall.cmd",
    "postInstallArguments": "\"{AppDirectory}\" {Version}",
    "allowFreshInstall": true,
    "compressFiles": true
  },
  "updatePolicy": {
    "mode": "optional",
    "minimumVersion": ""
  },
  "requiredRuntimes": [
    {
      "name": "Microsoft.WindowsDesktop.App",
      "version": "6.0.0",
      "type": "windowsdesktop-runtime"
    }
  ],
  "updateSource": {
    "type": "github",
    "repository": "owner/repo",
    "includePrerelease": false
  }
}
```

### `installation.extractionLayout`

Supported values:

- `auto`
- `direct`
- `strip-single-root-directory`

`auto` tries the archive root first and otherwise falls back to a single top-level directory when one exists.

### `installation.compressFiles`

When omitted, `compressFiles` defaults to `true`.

Compression is applied when the updater installs files from a downloaded package, including fresh installs and normal update installs.

### `requiredAppRuntimeVersion`

When set, the updater checks for a Windows App Runtime version greater than or equal to the configured version. If it is missing, the user is prompted before the updater downloads and installs the matching Microsoft bootstrapper.

### `requireWebView2Runtime`

When `true`, the updater checks for the Evergreen Microsoft Edge WebView2 Runtime before checking for app updates. If the runtime is missing, the user is prompted before the updater downloads and installs the Evergreen Bootstrapper from Microsoft.

### `updatePolicy.mode`

Supported values:

- `optional`
- `required`
- `minimum-version-required`

When `minimum-version-required` is used, `minimumVersion` must be a valid version string.

### `updateSource`

Supported source types:

- `github`
- `json`

For `github`, configure:

- `repository`: `owner/repo`
- `includePrerelease`: optional

For `json`, configure:

- `url`: absolute manifest URL

## Release Manifest

The updater expects a manifest named `release-manifest.json`.

Current manifest shape:

```json
{
  "applicationName": "My App",
  "version": "1.1.0",
  "updaterUpdateRequired": false,
  "download": {
    "kind": "application",
    "fileName": "MyApp-1.1.0.zip",
    "sha256": "..."
  },
  "applicationPackage": {
    "kind": "application",
    "fileName": "MyApp-1.1.0.zip",
    "sha256": "...",
    "files": [
      {
        "path": "MyApp.exe",
        "sha512": "...",
        "size": 123456
      }
    ]
  },
  "updaterPackage": {
    "kind": "installer",
    "fileName": "MyApp-1.1.0-setup.exe",
    "sha256": "..."
  }
}
```

Behavior:

- If `updaterUpdateRequired` is `false`, `download` points at the normal app package.
- If `updaterUpdateRequired` is `true`, `download` points at the updater-refresh installer.
- After an updater-refresh install, the stamped `updaterVersion` prevents the updater from looping back into the same installer release and allows it to fall back to the app package from the same manifest.
- For ZIP application packages, the CLI always includes per-file integrity data under `applicationPackage.files`. When integrity verification is enabled, the updater checks each extracted file's size and SHA-512 hash against this manifest.

For GitHub sources, the updater reads the latest eligible release, looks for a `release-manifest.json` asset, then resolves the referenced package asset by file name.

For JSON sources, relative `fileName` values are resolved relative to the manifest URL.
