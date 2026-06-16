# Cow Pilot

Cow Pilot is the C#/.NET Windows Forms replacement for the Java panel calculator.

## Download

For normal Windows use, open the private repo's `release` folder and download:

```text
CowPilot-1.1.4-win-x64.zip
```

Extract the zip and run `CowPilot.exe`.

The release zip is self-contained for Windows x64 and includes the .NET runtime. A separate .NET installer is not required.

## Update Check

On launch, Cow Pilot checks `release/latest.json` in this private repo and only shows a message when a newer version exists. It does not download, install, or auto-update anything.

Because the repo is private, GitHub may reject the version check unless the PC has a token available in `COW_PILOT_GITHUB_TOKEN` or `GITHUB_TOKEN`. If no token is available, Cow Pilot simply skips the update notification.

## Build

```powershell
dotnet build -c Release
```

## Self-test

```powershell
dotnet run -c Release -- --self-test
```

## Package Release Zip

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-release.ps1
```

The generated zip and `release\latest.json` are written under `release\` so they can be committed and downloaded directly from the private repo.

Current app version: `1.1.4`.
