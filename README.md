# AREA11a

## WPFSample (Windows / WPF C# test app)

A minimal WPF sample for Windows testing.
When opened it shows **"PDF is Working"** — white text on a red background.

### Requirements
- Windows 10/11
- .NET 8 SDK (or later): https://dotnet.microsoft.com/download

### Run it
```bash
cd WPFSample
dotnet run
```

Or open `WPFSample.sln` / `WPFSample.csproj` in Visual Studio and press F5.

### Files
- `WPFSample/MainWindow.xaml` — window UI (red background, white text)
- `WPFSample/MainWindow.xaml.cs` — code-behind
- `WPFSample/App.xaml` / `App.xaml.cs` — application entry point
- `WPFSample/WPFSample.csproj` — project file (net8.0-windows, WPF)
