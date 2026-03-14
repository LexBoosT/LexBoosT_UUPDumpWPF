# UUP Dump WPF - LexBoosT ISO Downloader

<a href="https://ibb.co/p6hn0tww"><img src="https://i.ibb.co/BHnwzX33/Capture-d-cran-2026-03-14-215332.png" alt="Capture-d-cran-2026-03-14-215332" border="0"></a>
<a href="https://ibb.co/Kz7mWkHQ"><img src="https://i.ibb.co/4gMJpGxD/Capture-d-cran-2026-03-14-215343.png" alt="Capture-d-cran-2026-03-14-215343" border="0"></a>
<a href="https://ibb.co/1tSbm2B5"><img src="https://i.ibb.co/W4Ts2K7w/Capture-d-cran-2026-03-14-215354.png" alt="Capture-d-cran-2026-03-14-215354" border="0"></a>
<a href="https://ibb.co/PsFB77Rk"><img src="https://i.ibb.co/RGYLxx18/Capture-d-cran-2026-03-14-224757.png" alt="Capture-d-cran-2026-03-14-224757" border="0"></a>

WPF application in C# with dark theme and Mica effect for downloading Windows ISOs via UUP Dump.

## Prerequisites

- **.NET 8.0 SDK** or higher
- **Windows 10/11** (Mica effect requires Windows 11)

## Build and Run

```bash
# Restore packages
dotnet restore

# Build
dotnet build

# Run
dotnet run
```

## Features

- **Build Search**: Search for Windows builds via UUP Dump API
- **Filters**: Filter by type (Retail/Preview) and architecture (amd64/arm64)
- **Language Selection**: Choose the ISO language
- **Edition Selection**: Choose Windows edition (Home, Pro, etc.)
- **Download Options**:
  - Include Updates
  - Cleanup
  - .NET 3.5
  - ESD Compression (Solid)
- **Mica Effect**: Modern theme with transparency on Windows 11
- **Dark Theme**: Dark interface for optimal visual comfort

## Project Structure

```
UUPDumpWPF/
├── Models/
│   └── Models.cs          # Data classes (Build, Language, Edition)
├── Services/
│   ├── UUPDumpService.cs  # UUP Dump API Service
│   └── WindowsVersionService.cs  # Windows version information
├── App.xaml               # Global resources and styles
├── MainWindow.xaml        # User interface
├── MainWindow.xaml.cs     # Business logic
└── UUPDumpWPF.csproj      # .NET Project
```

## APIs Used

- `https://api.uupdump.net/listid.php` - Build list
- `https://api.uupdump.net/listlangs.php` - Available languages
- `https://api.uupdump.net/listeditions.php` - Available editions

## Credits

- abbodi1406 `https://git.uupdump.net/abbodi1406`
- Kaenbyou Rin `https://git.uupdump.net/orin`
