# Changelog

## v1.3 - March 14, 2026

### 🌟 Major Features

#### 📦 Automatic Important_Files Download
- **Startup Download**: Automatically downloads necessary files at application startup if not present
- **Progress Overlay**: Beautiful download overlay with progress bar and real-time status
- **GitHub URL Fetch**: Dynamically fetches the latest download URL from GitHub repository
- **No Hardcoded Links**: Download URL is read from `UUP.txt` on GitHub (always up-to-date)
- **User-Friendly Messages**: Generic messages ("necessary files") instead of internal filenames

#### 🚗 Driver Integration System
- **One-Click Setup**: Click "Add Drivers" checkbox to open driver folder
- **Automatic Folder Creation**: Creates `Drivers` folder with `ALL`, `OS`, `WinPE` subfolders
- **Interactive Process**:
  1. Shows instructions for folder structure
  2. Opens Windows Explorer to the Drivers folder
  3. Wait for user to copy drivers
  4. Confirms and enables integration
- **Automatic Copy**: Drivers folder is copied to ISO build folder during creation
- **Relative Path**: Uses simple "Drivers" path compatible with UUP scripts

#### 📄 autounattend.xml Integration
- **New Checkbox**: "Add autounattend.xml" option in Additional Options section
- **Quick Link**: Direct link to Schneegans Unattend Generator (https://schneegans.de/windows/unattend-generator/)
- **Interactive Workflow**:
  1. Creates `autounattend` folder in temporary ISO directory
  2. Opens Windows Explorer to the folder
  3. User copies their autounattend.xml file
  4. User confirms when file is added
  5. File is automatically placed at ISO root
- **Smart Detection**: Automatically finds and copies any `.xml` file in the folder
- **Cancellation Support**: User can skip integration if needed
- **Final Location**: `autounattend.xml` at root of final ISO for Windows Setup auto-detection

#### 🔄 Auto-Retry System
- **Download Retries**: Automatic retry (up to 3 attempts) for failed downloads
- **Configurable Delay**: 5-second delay between retry attempts
- **Progress Messages**: Shows retry count (e.g., "Retry 2/3")
- **Applies To**:
  - Microsoft Store Apps download
  - UUP files download
  - Apps script retrieval
  - UUP script retrieval

#### 🎯 Build ID Management
- **Dynamic Build ID**: Automatically updates build ID in `uup_download_windows.cmd`
- **Placeholder System**: Uses `BUILD_ID_PLACEHOLDER`, `LANG_PLACEHOLDER`, `EDITION_PLACEHOLDER`
- **Automatic Replacement**: Replaces placeholders with selected build information
- **Title Update**: Updates window title with current build information

#### 🗑️ Smart Cancellation
- **Cancel Support**: Cancel button and window close (X) properly cancel operations
- **No False Errors**: Closing conversion window manually treated as cancellation, not error
- **Cleanup**: Automatically cleans up temporary folders on cancellation
- **User Confirmation**: Confirms before aborting download/creation process

### 🎨 UI Improvements

#### Simplified Interface
- **Removed TextBoxes**:
  - Removed "Drivers Path" TextBox (now automatic)
  - Removed "Virtual Editions" TextBox (now automatic)
- **Cleaner Layout**: Only "Add Drivers" checkbox visible in Additional Options
- **Centered Buttons**: App selection dialog buttons now centered

#### Improved Layout
- **Button Reordering**: "Make ISO" and "Download Update Only" buttons moved to right side
- **Destination on Left**: "Save to:" path field now on left side for better workflow
- **Wider Path TextBox**: Destination path field increased from 200px to 400px for better visibility
- **Better Spacing**: More balanced layout with elements pushed to opposite sides

#### Enhanced Tooltips
- **Apps Level**: Detailed description of each level (0-4) with exact app list
  - Level 0: All referenced Apps
  - Level 1: Store, Security Health, App Installer, StartExperiencesApp
  - Level 2: Level 1 + Photos, Camera, Notepad, Paint
  - Level 3: Level 2 + Terminal, Widgets, Mail/Outlook, Clipchamp
  - Level 4: Level 3 + Media apps (Music, Video, Codecs, Phone Link)
- **Full Stub Apps**: Clear explanation of stub vs full app installation
- **autounattend.xml**: Descriptive tooltip with workflow explanation

#### Smart Checkbox Logic
- **Skip Apps Integration**: When "Skip Apps" is checked:
  - Automatically unchecks "Use Custom List"
  - Disables and grays out "Full Stub Apps"
  - Disables and grays out "Apps Level" combo box
  - All options re-enabled when "Skip Apps" is unchecked

#### Download Progress UI
- **Dark Overlay**: Semi-transparent overlay during download
- **Progress Bar**: Visual progress indicator (0-100%)
- **Status Text**: Shows downloaded size / total size with percentage
- **File Size Formatting**: Automatic formatting (B, KB, MB, GB)

### 🐛 Bug Fixes

#### Download Issues
- **Missing Files Error**: Fixed "We couldn't find one of needed files" by ensuring all required files are in Important_Files.zip
- **Converter Exit Code**: Properly handles exit code -1073741510 (user closed window)
- **No More False Errors**: Manual window closure no longer shows as error

#### Build Issues
- **Project Cleanup**: Removed `CustomAppsList.txt` and `Important_Files.zip` from project build output
- **Clean Build**: Proper clean and rebuild without cached files

### ⚡ Performance Improvements

#### Startup Optimization
- **Single Download Check**: Files checked once at startup, not on every ISO creation
- **No Update Prompts**: Removed annoying update check during ISO creation
- **Faster ISO Creation**: Direct use of local files without redundant checks

#### Download Efficiency
- **Chunked Download**: 8KB chunks for better memory management
- **Progress Reporting**: Real-time progress updates via Dispatcher
- **Timeout Handling**: 10-minute timeout for large downloads

### 🔧 Technical Changes

#### Code Structure
- **New Methods**:
  - `CheckAndDownloadImportantFilesAsync()` - Startup download check
  - `GetLatestImportantFilesUrlAsync()` - Fetch URL from GitHub
  - `DownloadImportantFilesAsync()` - Download with progress support
  - `CopyDirectory()` - Recursive directory copy
  - `FormatFileSize()` - Human-readable file size formatting

#### Configuration
- **GitHub URL**: `https://raw.githubusercontent.com/LexBoosT/LexBoosT-s-Tweaks/refs/heads/master/UUP.txt`
- **Default Drivers Path**: `"Drivers"` (relative path)
- **Max Retries**: 3 attempts
- **Retry Delay**: 5 seconds

#### Script Modifications
- **uup_download_windows.cmd**:
  - Removed UUP converter download (files already included)
  - Added retry logic for all downloads
  - Simplified file checks
  - Updated placeholder system

### 📝 Documentation

#### CHANGELOG Updates
- Comprehensive list of all v1.3 changes
- Organized by category (Features, UI, Fixes, Performance, Technical)

### 🎯 User Experience

#### Before (v1.2)
- ❌ Manual file management
- ❌ No download progress
- ❌ Errors on window close
- ❌ Visible configuration textboxes
- ❌ Update prompts during ISO creation

#### After (v1.3)
- ✅ Automatic file download at startup
- ✅ Beautiful progress overlay
- ✅ Proper cancellation handling
- ✅ Clean, simplified interface
- ✅ Single check at startup only

### 📦 Files Changed

#### Modified Files
- `MainWindow.xaml.cs` - Download logic, driver integration, cancellation
- `MainWindow.xaml` - Download overlay, simplified UI
- `AppSelectionDialog.xaml.cs` - Updated app dictionary based on CustomAppsList.txt
- `AppSelectionDialog.xaml` - Centered buttons
- `uup_download_windows.cmd` - Retry system, removed converter download
- `UUPDumpWPF.csproj` - Removed bundled files
- `VirtualEditionsDialog.xaml.cs` - Simplified virtual editions handling

#### New Features in Important_Files.zip
- All required binaries in `bin/` folder
- Clean `uup_download_windows.cmd` with placeholders
- Organized folder structure

---

## v1.2 - March 13, 2026

### 🐛 Bug Fixes
- **ARM64 Support**: Fixed arm64 builds not appearing in search results
- **Architecture API**: Now using UUP Dump API directly to fetch architecture (faster and more reliable)

### ✨ New Features
- **Real-time Filtering**: Architecture (amd64/arm64) and Type (Retail/Preview) filters apply instantly without re-searching
- **Color-coded Display**: AMD64 in red and ARM64 in green with brackets `[AMD64]` / `[ARM64]`
- **Custom ISO Naming**: Format `Windows{Version}_{Edition}_{FeatureVersion}_{BuildNumber}_{Language}.iso`
  - Example: `Windows11_PRO_25H2_26200.8106_fr-fr.iso`

### 🎨 UI Improvements
- **Rounded Corners**: All panels, buttons, lists, and text fields now have 8px rounded corners
- **Architecture Label**: Now bold and underlined

### ⚡ Performance
- **API Calls**: Reduced API requests (architecture included in initial response)
- **User Experience**: Instant filtering for smoother navigation
