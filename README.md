# Cow Pilot

Cow Pilot is the C#/.NET Windows Forms replacement for the Java panel calculator.

## Build

```powershell
dotnet build -c Release
```

## Self-test

```powershell
dotnet run -- --self-test
```

## Publish

```powershell
dotnet publish CowPilot.csproj -c Release -o publish\1.0.4 -p:PublishSingleFile=true --self-contained false
```

Current app version: `1.0.4`.
