using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using UUPDumpWPF.Models;
using UUPDumpWPF.Services;

namespace UUPDumpWPF
{
    public partial class MainWindow : Window
    {
        // P/Invoke for Mica and Immersive Dark Mode
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_MICA_EFFECT = 1029;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMSBT_MAINWINDOW = 2; // Mica

        // GitHub URL to fetch the latest Important_Files.zip download link
        private const string UUPConfigUrl = "https://raw.githubusercontent.com/LexBoosT/LexBoosT-s-Tweaks/refs/heads/master/UUP.txt";

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private readonly UUPDumpService _uupService;
        private readonly WindowsVersionService _versionService;

        private List<Build> _allBuilds = new();
        private List<Build> _currentBuilds = new();
        private List<Language> _currentLanguages = new();
        private List<Edition> _currentEditions = new();
        private Build? _selectedBuild;

        // Public access to CheckBox for VirtualEditionsDialog
        public CheckBox ChkStartVirtualPublic => ChkStartVirtual;

        public MainWindow()
        {
            InitializeComponent();

            _uupService = new UUPDumpService();
            _versionService = new WindowsVersionService();

            // Set default destination
            TxtDestination.Text = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads", "UUP-ISOs");

            // Apply Mica effect and load Windows version after window is loaded
            this.Loaded += async (s, e) =>
            {
                ApplyMicaEffect();
                LoadCurrentVersion();
                await CheckForNewerBuildAsync();
                await CheckAndDownloadImportantFilesAsync();
            };
        }

        private void ApplyMicaEffect()
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;

            // Enable dark mode title bar (Windows 10 20H2+)
            int darkMode = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, Marshal.SizeOf<int>());

            // Enable Mica for Windows 11
            if (Environment.OSVersion.Version.Build >= 22000)
            {
                // Try newer method first (Windows 11 22H2+)
                int backdropType = DWMSBT_MAINWINDOW;
                var result = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, Marshal.SizeOf<int>());

                if (result != 0)
                {
                    // Fallback to older method
                    int mica = 1;
                    DwmSetWindowAttribute(hwnd, DWMWA_MICA_EFFECT, ref mica, Marshal.SizeOf<int>());
                }

                // Set window background to transparent for Mica to show through
                // But keep child controls with their own backgrounds
                if (result == 0)
                {
                    Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
                }
            }
        }

        private void LoadCurrentVersion()
        {
            try
            {
                var version = _versionService.GetCurrentVersion();
                LblCurrentVersionInfo.Text = version.FullVersion;
            }
            catch (Exception ex)
            {
                LblCurrentVersionInfo.Text = $"Error: {ex.Message}";
            }
        }

        private async Task CheckForNewerBuildAsync()
        {
            try
            {
                // Get current build number
                var currentVersion = _versionService.GetCurrentVersion();
                var currentBuildParts = currentVersion.Build.Split('.');
                if (!int.TryParse(currentBuildParts[0], out int currentMajor) || currentBuildParts.Length < 2)
                    return;
                if (!int.TryParse(currentBuildParts[1], out int currentMinor))
                    return;

                // Search for newer builds on UUP Dump (fast - only top 50)
                var builds = await _uupService.GetBuildsAsync("windows 11");

                if (builds.Count == 0)
                    return;

                // Find the highest build number for the same major version
                Build? highestBuild = null;
                int highestMinor = -1;

                foreach (var build in builds)
                {
                    var buildParts = build.BuildNumber.Split('.');
                    if (buildParts.Length < 2)
                        continue;

                    if (!int.TryParse(buildParts[0], out int buildMajor))
                        continue;

                    // Only consider builds with the same major version
                    if (buildMajor != currentMajor)
                        continue;

                    if (!int.TryParse(buildParts[1], out int buildMinor))
                        continue;

                    // Check if this build has a higher minor version
                    if (buildMinor > highestMinor)
                    {
                        highestMinor = buildMinor;
                        highestBuild = build;
                    }
                }

                if (highestBuild == null)
                    return;

                // Check if the highest build is newer than current
                if (highestMinor > currentMinor)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        var result = MessageBox.Show(
                            $"A newer build is available!\n\n" +
                            $"Your build: {currentVersion.Build}\n" +
                            $"Latest build: {highestBuild.BuildNumber}\n\n" +
                            $"Title: {highestBuild.Title}\n\n" +
                            $"Would you like to search for this build?",
                            "New Build Available",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            TxtSearch.Text = "windows 11";
                            BtnSearch_Click(null!, null!);
                        }
                    });
                }
            }
            catch
            {
                // Silently fail
            }
        }

        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            var searchQuery = TxtSearch.Text.Trim();
            if (string.IsNullOrEmpty(searchQuery))
                return;

            LblStatus.Text = "Searching...";
            LblStatus.Foreground = Brushes.Gray;
            BtnSearch.IsEnabled = false;

            try
            {
                var builds = await _uupService.GetBuildsAsync(searchQuery);

                // Store all builds from search
                _allBuilds = builds;

                // Apply all filters
                var filteredBuilds = ApplyFilters(builds);

                _currentBuilds = filteredBuilds;
                LstBuilds.ItemsSource = filteredBuilds;

                LblStatus.Text = $"Found {filteredBuilds.Count} builds";
                LblStatus.Foreground = Brushes.LightGreen;
            }
            catch (Exception ex)
            {
                LblStatus.Text = "Search failed";
                LblStatus.Foreground = Brushes.OrangeRed;
                MessageBox.Show($"Error: {ex.Message}", "Search Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSearch.IsEnabled = true;
            }
        }

        private void ArchitectureFilter_Changed(object sender, RoutedEventArgs e)
        {
            // Re-apply filters when architecture checkboxes change
            if (_allBuilds == null || _allBuilds.Count == 0)
                return;

            var filteredBuilds = ApplyFilters(_allBuilds);

            _currentBuilds = filteredBuilds;
            LstBuilds.ItemsSource = filteredBuilds;

            LblStatus.Text = $"Found {filteredBuilds.Count} builds";
            LblStatus.Foreground = Brushes.LightGreen;
        }

        private void RetailFilter_Changed(object sender, RoutedEventArgs e)
        {
            // Re-apply filters when retail/preview checkboxes change
            if (_allBuilds == null || _allBuilds.Count == 0)
                return;

            var filteredBuilds = ApplyFilters(_allBuilds);

            _currentBuilds = filteredBuilds;
            LstBuilds.ItemsSource = filteredBuilds;

            LblStatus.Text = $"Found {filteredBuilds.Count} builds";
            LblStatus.Foreground = Brushes.LightGreen;
        }

        private List<Build> ApplyFilters(List<Build> builds)
        {
            return builds.Where(b =>
            {
                // Retail filter
                var retailMatch = (ChkRetail.IsChecked == true && b.IsRetail) ||
                                 (ChkPreview.IsChecked == true && !b.IsRetail);

                // Skip builds with unknown architecture
                if (b.Architecture == "unknown" || string.IsNullOrEmpty(b.Architecture))
                    return false;

                // Architecture filter
                var archMatch = (ChkAmd64.IsChecked == true && b.Architecture == "amd64") ||
                               (ChkArm64.IsChecked == true && b.Architecture == "arm64");

                return retailMatch && archMatch;
            }).ToList();
        }

        private async void LstBuilds_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstBuilds.SelectedIndex < 0 || LstBuilds.SelectedIndex >= _currentBuilds.Count)
                return;

            _selectedBuild = _currentBuilds[LstBuilds.SelectedIndex];

            // Clear dependent lists
            LstLanguages.ItemsSource = null;
            LstEditions.ItemsSource = null;
            _currentLanguages.Clear();
            _currentEditions.Clear();

            // Load languages
            LblStatus.Text = "Loading languages...";
            try
            {
                var languages = await _uupService.GetLanguagesAsync(_selectedBuild.Id);
                _currentLanguages = languages.OrderBy(l => l.Name).ToList();
                LstLanguages.ItemsSource = _currentLanguages.Select(l => $"{l.Name} ({l.Code})");
                LblStatus.Text = $"Loaded {_currentLanguages.Count} languages";
            }
            catch (Exception ex)
            {
                LblStatus.Text = "Failed to load languages";
                MessageBox.Show($"Error loading languages: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LstLanguages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstLanguages.SelectedIndex < 0 || LstLanguages.SelectedIndex >= _currentLanguages.Count)
                return;

            // Clear editions
            LstEditions.ItemsSource = null;
            _currentEditions.Clear();

            var selectedLanguage = _currentLanguages[LstLanguages.SelectedIndex];

            // Load editions
            LblStatus.Text = "Loading editions...";
            try
            {
                var editions = await _uupService.GetEditionsAsync(_selectedBuild!.Id, selectedLanguage.Code);
                _currentEditions = editions.OrderBy(e => e.Name).ToList();
                LstEditions.ItemsSource = _currentEditions.Select(e => 
                    $"{e.Name} ({e.Code}){(e.IsVirtual ? " [Virtual]" : "")}");
                LblStatus.Text = $"Loaded {_currentEditions.Count} editions";
            }
            catch (Exception ex)
            {
                LblStatus.Text = "Failed to load editions";
                MessageBox.Show($"Error loading editions: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LstEditions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstEditions.SelectedIndex < 0 || LstEditions.SelectedIndex >= _currentEditions.Count)
                return;

            var selectedEdition = _currentEditions[LstEditions.SelectedIndex];

            // Check if Pro edition is selected
            bool isProSelected = selectedEdition.Code.Equals("professional", StringComparison.OrdinalIgnoreCase);

            if (isProSelected)
            {
                // Enable virtual editions options
                SetVirtualEditionsOptionsEnabled(true);
                SetStoreAppsOptionsEnabled(true);
                
                // If virtual editions are already configured, keep them
                // User can click "Create Virtual Editions" checkbox to change selection
            }
            else
            {
                // Non-Pro edition selected - disable and clear virtual editions
                ChkStartVirtual.IsChecked = false;
                SetVirtualEditionsOptionsEnabled(false);
                SetStoreAppsOptionsEnabled(true);
            }

            LblStatus.Text = "Ready to make ISO or Download Update";
            LblStatus.Foreground = Brushes.LightGreen;
        }

        private void ChkStartVirtual_Checked(object sender, RoutedEventArgs e)
        {
            // Only show dialog if Pro edition is selected
            if (LstEditions.SelectedIndex >= 0 && LstEditions.SelectedIndex < _currentEditions.Count)
            {
                var selectedEdition = _currentEditions[LstEditions.SelectedIndex];

                if (selectedEdition.Code.Equals("professional", StringComparison.OrdinalIgnoreCase))
                {
                    var dialog = new VirtualEditionsDialog(this, "");
                    var result = dialog.ShowDialog();

                    if (result != true)
                    {
                        // User cancelled or didn't select any editions
                        ChkStartVirtual.IsChecked = false;
                        SetStoreAppsOptionsEnabled(true);
                    }
                    else
                    {
                        // Virtual editions selected - disable Store Apps options
                        SetStoreAppsOptionsEnabled(false);
                    }
                }
                else
                {
                    // Not Pro edition - can't create virtual editions
                    MessageBox.Show(
                        "Virtual editions are only available for Windows Pro.\n\n" +
                        "Please select Windows Pro to create virtual editions.",
                        "Virtual Editions Not Available",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    
                    ChkStartVirtual.IsChecked = false;
                }
            }
        }

        private void ChkStartVirtual_Unchecked(object sender, RoutedEventArgs e)
        {
            // When unchecked, re-enable Store Apps options
            SetStoreAppsOptionsEnabled(true);
        }

        private void ChkAddDrivers_Checked(object sender, RoutedEventArgs e)
        {
            // Show explanation dialog
            var result = MessageBox.Show(
                "Driver Integration - Folder Structure:\n\n" +
                "ALL   → Drivers added to ALL WIM files (boot.wim, install.wim, winre.wim)\n" +
                "OS    → Drivers added to install.wim ONLY (Windows installation)\n" +
                "WinPE → Drivers added to boot.wim / winre.wim ONLY (Pre-installation environment)\n\n" +
                "Instructions:\n" +
                "1. Click OK to open the Drivers folder\n" +
                "2. Copy your driver folders (.inf files) into ALL, OS, or WinPE\n" +
                "3. Close the Drivers folder when finished\n" +
                "4. Click OK again to confirm and enable driver integration\n\n" +
                "Note: Drivers must be extracted (.inf files), not .exe installers.",
                "Add Drivers - Instructions",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.OK)
            {
                // User cancelled - uncheck the box
                ChkAddDrivers.IsChecked = false;
                return;
            }

            // Use the default Drivers folder path (will be created in the ISO folder during build)
            var driversPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Drivers");

            // Create the folder if it doesn't exist
            if (!System.IO.Directory.Exists(driversPath))
            {
                System.IO.Directory.CreateDirectory(driversPath);

                // Create subfolders
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(driversPath, "ALL"));
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(driversPath, "OS"));
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(driversPath, "WinPE"));
            }

            // Open the folder in Explorer
            System.Diagnostics.Process.Start("explorer.exe", driversPath);

            // Wait for user to close the folder and confirm
            var confirmResult = MessageBox.Show(
                $"Drivers folder opened:\n{driversPath}\n\n" +
                "Have you finished copying your drivers?\n\n" +
                "Click OK to enable driver integration and start the ISO creation.\n" +
                "Click Cancel to disable driver integration.",
                "Confirm Driver Integration",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.OK)
            {
                // User cancelled - uncheck the box
                ChkAddDrivers.IsChecked = false;
            }
        }

        private void ChkAddDrivers_Unchecked(object sender, RoutedEventArgs e)
        {
            // Nothing to do - path is handled automatically
        }

        private void ChkSkipApps_Checked(object sender, RoutedEventArgs e)
        {
            // When Skip Apps is checked, disable and uncheck other app options
            ChkCustomList.IsChecked = false;
            ChkCustomList.IsEnabled = false;
            
            ChkStubAppsFull.IsChecked = false;
            ChkStubAppsFull.IsEnabled = false;
            
            CmbAppsLevel.IsEnabled = false;
        }

        private void ChkSkipApps_Unchecked(object sender, RoutedEventArgs e)
        {
            // When Skip Apps is unchecked, re-enable other app options
            ChkCustomList.IsEnabled = true;
            ChkStubAppsFull.IsEnabled = true;
            CmbAppsLevel.IsEnabled = true;
        }

        private void SetVirtualEditionsOptionsEnabled(bool isEnabled)
        {
            ChkStartVirtual.IsEnabled = isEnabled;
            ChkVUseDism.IsEnabled = isEnabled;
            ChkVAutoStart.IsEnabled = isEnabled;
            ChkVDeleteSource.IsEnabled = isEnabled;
            ChkVPreserve.IsEnabled = isEnabled;
            ChkVwim2esd.IsEnabled = isEnabled;
            ChkVwim2swm.IsEnabled = isEnabled;
            ChkVSkipISO.IsEnabled = isEnabled;

            if (!isEnabled)
            {
                ChkStartVirtual.IsChecked = false;
            }
        }

        private void SetStoreAppsOptionsEnabled(bool isEnabled)
        {
            ChkSkipApps.IsEnabled = isEnabled;
            ChkCustomList.IsEnabled = isEnabled;
            ChkStubAppsFull.IsEnabled = isEnabled;
            CmbAppsLevel.IsEnabled = isEnabled;
            
            if (!isEnabled)
            {
                // Disable apps integration when virtual editions are selected
                ChkSkipApps.IsChecked = true;
                ChkCustomList.IsChecked = false;
                ChkStubAppsFull.IsChecked = false;
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder to save ISO/Update files",
                SelectedPath = TxtDestination.Text,
                ShowNewFolderButton = true
            };

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtDestination.Text = folderDialog.SelectedPath;
            }
        }

        private async void BtnDownloadPack_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBuild == null)
            {
                MessageBox.Show("Please select a build first.", "Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (LstLanguages.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a language.", "Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (LstEditions.SelectedIndex < 0)
            {
                MessageBox.Show("Please select an edition.", "Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedLanguage = _currentLanguages[LstLanguages.SelectedIndex];
            var selectedEdition = _currentEditions[LstEditions.SelectedIndex];

            var message = $"Build: {_selectedBuild.Title}\n";
            message += $"Language: {selectedLanguage.Name}\n";
            message += $"Edition: {selectedEdition.Name}\n\n";

            if (ChkStartVirtual.IsChecked == true)
            {
                message += $"*** VIRTUAL EDITIONS ***\n";
                message += $"Will create virtual editions from Pro\n\n";
            }

            message += "This will download the update pack.\n";
            message += "\n\nContinue?";

            var result = MessageBox.Show(message, "Confirmation",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            await StartDownloadPackAsync(_selectedBuild, selectedLanguage, selectedEdition);
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBuild == null)
            {
                MessageBox.Show("Please select a build first.", "Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (LstLanguages.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a language.", "Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (LstEditions.SelectedIndex < 0)
            {
                MessageBox.Show("Please select an edition.", "Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedLanguage = _currentLanguages[LstLanguages.SelectedIndex];
            var selectedEdition = _currentEditions[LstEditions.SelectedIndex];

            var optionsInfo = "";
            if (ChkAutoStart.IsChecked == true) optionsInfo += "AutoStart, ";
            if (ChkUpdates.IsChecked == true) optionsInfo += "Updates, ";
            if (ChkCleanup.IsChecked == true) optionsInfo += "Cleanup, ";
            if (ChkResetBase.IsChecked == true) optionsInfo += "ResetBase, ";
            if (ChkNetFx3.IsChecked == true) optionsInfo += ".NET 3.5, ";
            if (ChkSolid.IsChecked == true) optionsInfo += "ESD, ";
            if (ChkSkipEdge.IsChecked == true) optionsInfo += "Skip Edge, ";
            if (ChkSkipISO.IsChecked == true) optionsInfo += "Skip ISO, ";
            if (ChkSkipWinRE.IsChecked == true) optionsInfo += "Skip WinRE, ";
            if (ChkLCUwinre.IsChecked == true) optionsInfo += "LCU WinRE, ";
            if (ChkUpdtBootFiles.IsChecked == true) optionsInfo += "Updt BootFiles, ";
            if (ChkForceDism.IsChecked == true) optionsInfo += "Force DISM, ";
            if (ChkRefESD.IsChecked == true) optionsInfo += "Ref ESD, ";
            if (ChkSkipLCUmsu.IsChecked == true) optionsInfo += "Skip LCUmsu, ";
            if (ChkAutoExit.IsChecked == true) optionsInfo += "AutoExit, ";
            if (ChkDisableUpdatingUpgrade.IsChecked == true) optionsInfo += "DisableUpdatingUpgrade, ";
            if (ChkWim2Swm.IsChecked == true) optionsInfo += "Split WIM, ";
            if (ChkSkipApps.IsChecked == true) optionsInfo += "Skip Apps, ";
            if (ChkCustomList.IsChecked == true) optionsInfo += "CustomList, ";
            if (ChkStubAppsFull.IsChecked == true) optionsInfo += "StubAppsFull, ";
            optionsInfo += $"AppsLevel:{CmbAppsLevel.SelectedIndex}, ";
            if (ChkStartVirtual.IsChecked == true) optionsInfo += "StartVirtual, ";
            if (ChkVUseDism.IsChecked == true) optionsInfo += "VUseDism, ";
            if (ChkVAutoStart.IsChecked == true) optionsInfo += "VAutoStart, ";
            if (ChkVDeleteSource.IsChecked == true) optionsInfo += "VDeleteSource, ";
            if (ChkVPreserve.IsChecked == true) optionsInfo += "VPreserve, ";
            if (ChkVwim2esd.IsChecked == true) optionsInfo += "Vwim2esd, ";
            if (ChkVwim2swm.IsChecked == true) optionsInfo += "Vwim2swm, ";
            if (ChkVSkipISO.IsChecked == true) optionsInfo += "VSkipISO, ";
            if (ChkAddDrivers.IsChecked == true) optionsInfo += "AddDrivers, ";
            optionsInfo = optionsInfo.TrimEnd(',', ' ');
            if (string.IsNullOrEmpty(optionsInfo)) optionsInfo = "Default options";

            var message = $"Build: {_selectedBuild.Title}\n";
            message += $"Language: {selectedLanguage.Name}\n";
            message += $"Edition: {selectedEdition.Name}\n";

            if (ChkStartVirtual.IsChecked == true)
            {
                message += $"\n*** VIRTUAL EDITIONS ***\n";
                message += $"Will create virtual editions from Pro\n";
            }

            message += $"Options: {optionsInfo}\n";
            message += $"Destination: {TxtDestination.Text}\n\n";
            message += "This will download and create the ISO automatically.\nThis may take a while, depending on your connection.\n\nContinue?";

            var result = MessageBox.Show(message, "Confirmation",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            await StartDownloadAsync(_selectedBuild, selectedLanguage, selectedEdition);
        }

        private async Task StartDownloadPackAsync(Build build, Language language, Edition edition)
        {
            var destination = TxtDestination.Text;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var tempDir = System.IO.Path.Combine(destination, $"UUP_update_{timestamp}");

            try
            {
                // Create directories
                System.IO.Directory.CreateDirectory(destination);
                if (System.IO.Directory.Exists(tempDir))
                {
                    System.IO.Directory.Delete(tempDir, true);
                }
                System.IO.Directory.CreateDirectory(tempDir);

                LblStatus.Text = "Downloading update package from UUP dump...";
                LblStatus.Foreground = Brushes.SkyBlue;
                BtnDownloadPack.IsEnabled = false;

                var zipPath = System.IO.Path.Combine(tempDir, "update_package.zip");

                // Download package from UUP dump
                // URL format: get.php?id={buildId}&pack=0&edition=updateOnly&autodl=1
                var downloadUrl = $"https://uupdump.net/get.php?id={build.Id}&pack=0&edition=updateOnly&autodl=1";

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                    
                    LblStatus.Text = "Connecting to UUP dump...";
                    var fileData = await httpClient.GetByteArrayAsync(downloadUrl);
                    
                    LblStatus.Text = "Saving downloaded package...";
                    await System.IO.File.WriteAllBytesAsync(zipPath, fileData);
                }

                LblStatus.Text = "Extracting package...";

                // Extract zip
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir, true);
                System.IO.File.Delete(zipPath);

                LblStatus.Text = "Running uup_download_windows.cmd...";
                LblStatus.Foreground = Brushes.LightGreen;

                // Run uup_download_windows.cmd from the extracted folder
                var downloaderScript = System.IO.Path.Combine(tempDir, "uup_download_windows.cmd");
                if (!System.IO.File.Exists(downloaderScript))
                {
                    throw new Exception("uup_download_windows.cmd not found in package");
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"uup_download_windows.cmd\"",
                    WorkingDirectory = tempDir,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        process.WaitForExit();

                        // Exit code -1073741510 (0xC000013A) means user closed the window manually
                        // Exit code -1073741502 (0xC000013E) also means window closed
                        // Don't throw error if user closed the window manually
                        if (process.ExitCode != 0 && process.ExitCode != -1073741510 && process.ExitCode != -1073741502)
                        {
                            throw new Exception($"Download script failed with exit code {process.ExitCode}");
                        }
                        else if (process.ExitCode == -1073741510 || process.ExitCode == -1073741502)
                        {
                            // User closed the window manually - treat as cancelled
                            LblStatus.Text = "Download cancelled by user";
                            LblStatus.Foreground = Brushes.OrangeRed;
                            MessageBox.Show(
                                "Download was cancelled by closing the conversion window.\n\n" +
                                $"Temporary files are located at:\n{tempDir}",
                                "Cancelled",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                            return;
                        }
                    }
                }

                // Wait for UUPs folder to be created (downloaded files)
                LblStatus.Text = "Waiting for download to complete...";
                var uupsFolder = System.IO.Path.Combine(tempDir, "UUPs");
                var maxWaitTime = TimeSpan.FromMinutes(60);
                var waitInterval = TimeSpan.FromSeconds(5);
                var startTime = DateTime.Now;

                while (!System.IO.Directory.Exists(uupsFolder))
                {
                    if (DateTime.Now - startTime > maxWaitTime)
                    {
                        throw new Exception("Timeout waiting for UUPs folder to be created");
                    }
                    await Task.Delay(waitInterval);
                }

                // Short delay to ensure files are fully written
                await Task.Delay(3000);

                LblStatus.Text = "Downloading W10UI from GitHub...";

                // Download W10UI files from GitHub repository
                var win10uiFolder = System.IO.Path.Combine(tempDir, "W10UI");
                if (!System.IO.Directory.Exists(win10uiFolder))
                {
                    System.IO.Directory.CreateDirectory(win10uiFolder);
                }

                var w10uiFiles = new[] { "W10UI.cmd", "W10UI.ini" };
                var githubBaseUrl = "https://raw.githubusercontent.com/abbodi1406/BatUtil/master/W10UI/";

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                    var downloadTasks = w10uiFiles.Select(async fileName =>
                    {
                        try
                        {
                            var fileUrl = githubBaseUrl + fileName;
                            var filePath = System.IO.Path.Combine(win10uiFolder, fileName);
                            var fileData = await httpClient.GetByteArrayAsync(fileUrl);
                            await System.IO.File.WriteAllBytesAsync(filePath, fileData);
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    });

                    var results = await Task.WhenAll(downloadTasks);
                    var downloadedCount = results.Count(r => r);

                    if (downloadedCount == 0)
                    {
                        try
                        {
                            var fileUrl = githubBaseUrl + "W10UI.cmd";
                            var filePath = System.IO.Path.Combine(win10uiFolder, "W10UI.cmd");
                            var fileData = await httpClient.GetByteArrayAsync(fileUrl);
                            await System.IO.File.WriteAllBytesAsync(filePath, fileData);
                            downloadedCount = 1;
                        }
                        catch { }
                    }

                    if (downloadedCount == 0)
                    {
                        throw new Exception("Failed to download W10UI files from GitHub");
                    }
                }

                // Copy all files from W10UI to UUPs
                foreach (var file in System.IO.Directory.GetFiles(win10uiFolder, "*.*", System.IO.SearchOption.AllDirectories))
                {
                    var relativePath = System.IO.Path.GetRelativePath(win10uiFolder, file);
                    var destFile = System.IO.Path.Combine(uupsFolder, relativePath);
                    var destDir = System.IO.Path.GetDirectoryName(destFile);

                    if (!string.IsNullOrEmpty(destDir) && !System.IO.Directory.Exists(destDir))
                    {
                        System.IO.Directory.CreateDirectory(destDir);
                    }

                    System.IO.File.Copy(file, destFile, true);
                }

                // Delete the temporary W10UI folder
                try
                {
                    System.IO.Directory.Delete(win10uiFolder, true);
                }
                catch { /* Ignore deletion errors */ }

                LblStatus.Text = "Launching W10UI.cmd...";

                // Find and run W10UI.cmd
                var w10uiScript = System.IO.Path.Combine(uupsFolder, "W10UI.cmd");
                if (!System.IO.File.Exists(w10uiScript))
                {
                    var w10uiFilesFound = System.IO.Directory.GetFiles(uupsFolder, "W10UI.cmd", System.IO.SearchOption.AllDirectories);
                    w10uiScript = w10uiFilesFound.FirstOrDefault() ?? null;
                }

                if (System.IO.File.Exists(w10uiScript))
                {
                    LblStatus.Text = "Installing update...";
                    LblStatus.Foreground = Brushes.LightGreen;

                    var w10uiProcessInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"cd /d \"{System.IO.Path.GetDirectoryName(w10uiScript)}\" && W10UI.cmd\"",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal
                    };

                    using (var w10uiProcess = Process.Start(w10uiProcessInfo))
                    {
                        if (w10uiProcess != null)
                        {
                            w10uiProcess.WaitForExit();

                            if (w10uiProcess.ExitCode != 0)
                            {
                                throw new Exception($"W10UI.cmd failed with exit code {w10uiProcess.ExitCode}");
                            }
                        }
                    }

                    LblStatus.Text = "W10UI completed successfully!";
                    LblStatus.Foreground = Brushes.LightGreen;

                    MessageBox.Show(
                        $"Update installation completed successfully!\n\nFolder: {tempDir}",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    throw new Exception("W10UI.cmd not found in UUPs folder");
                }

                LblStatus.Text = "Download completed successfully!";
                LblStatus.Foreground = Brushes.LightGreen;
            }
            catch (Exception ex)
            {
                LblStatus.Text = "Download pack failed";
                LblStatus.Foreground = Brushes.OrangeRed;
                MessageBox.Show($"Download pack failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnDownloadPack.IsEnabled = true;
            }
        }

        private async Task StartDownloadAsync(Build build, Language language, Edition edition)
        {
            var destination = TxtDestination.Text;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var tempDir = System.IO.Path.Combine(destination, $"UUP_{build.Id}_{timestamp}");

            try
            {
                // Create directories
                System.IO.Directory.CreateDirectory(destination);
                if (System.IO.Directory.Exists(tempDir))
                {
                    System.IO.Directory.Delete(tempDir, true);
                }
                System.IO.Directory.CreateDirectory(tempDir);

                LblStatus.Text = "Preparing Important_Files.zip...";
                LblStatus.Foreground = Brushes.SkyBlue;
                BtnDownload.IsEnabled = false;

                var zipPath = System.IO.Path.Combine(tempDir, "Important_Files.zip");

                // File should already exist (downloaded at startup in Temp folder)
                var importantFilesZipPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LexBoosT_UUPDumpWPF", "Important_Files.zip");
                if (!System.IO.File.Exists(importantFilesZipPath))
                {
                    throw new Exception("Necessary files not found. Please restart the application to download them.");
                }

                // Copy the zip to temp directory
                System.IO.File.Copy(importantFilesZipPath, zipPath, true);

                LblStatus.Text = "Extracting package...";

                // Extract zip
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir, true);
                System.IO.File.Delete(zipPath);

                // Update build ID in uup_download_windows.cmd
                LblStatus.Text = "Updating build ID...";
                var downloaderScript = System.IO.Path.Combine(tempDir, "uup_download_windows.cmd");
                if (System.IO.File.Exists(downloaderScript))
                {
                    var cmdContent = System.IO.File.ReadAllText(downloaderScript);

                    // Update title line
                    var buildNumber = build.BuildNumber.Replace('.', '_');
                    var arch = build.Architecture.ToLower();
                    var langCode = language.Code.ToLower();
                    var editionCode = edition.Code.ToLower();
                    var buildIdShort = build.Id.Split('-').Last(); // Get last part of GUID like 05f1b1e6
                    
                    var titlePattern = @"^title\s+.*";
                    var titleReplacement = $"title {buildNumber}_{arch}_{langCode}_{editionCode}_{buildIdShort} download";
                    cmdContent = System.Text.RegularExpressions.Regex.Replace(cmdContent, titlePattern, titleReplacement, System.Text.RegularExpressions.RegexOptions.Multiline);

                    // Update BUILD_ID_PLACEHOLDER
                    cmdContent = cmdContent.Replace("BUILD_ID_PLACEHOLDER", build.Id);

                    // Update LANG_PLACEHOLDER
                    cmdContent = cmdContent.Replace("LANG_PLACEHOLDER", language.Code);

                    // Update EDITION_PLACEHOLDER
                    cmdContent = cmdContent.Replace("EDITION_PLACEHOLDER", edition.Code.ToUpper());

                    System.IO.File.WriteAllText(downloaderScript, cmdContent);
                }

                LblStatus.Text = "Processing configuration...";

                // Copy our comprehensive CustomAppsList.txt to replace the UUP dump one
                var sourceCustomAppsList = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "CustomAppsList.txt");
                var targetCustomAppsList = System.IO.Path.Combine(tempDir, "CustomAppsList.txt");

                if (System.IO.File.Exists(sourceCustomAppsList))
                {
                    try
                    {
                        System.IO.File.Copy(sourceCustomAppsList, targetCustomAppsList, true);
                    }
                    catch (Exception ex)
                    {
                        // Ignore errors if we can't copy the file
                        System.Diagnostics.Debug.WriteLine($"Failed to copy CustomAppsList.txt: {ex.Message}");
                    }
                }

                // Copy Drivers folder if Add Drivers option is enabled
                if (ChkAddDrivers.IsChecked == true)
                {
                    LblStatus.Text = "Copying drivers...";
                    var sourceDriversPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Drivers");
                    var targetDriversPath = System.IO.Path.Combine(tempDir, "Drivers");

                    if (System.IO.Directory.Exists(sourceDriversPath))
                    {
                        try
                        {
                            CopyDirectory(sourceDriversPath, targetDriversPath, true);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to copy Drivers folder: {ex.Message}");
                        }
                    }
                }

                // Handle autounattend.xml if Add autounattend option is enabled
                if (ChkAddAutounattend.IsChecked == true)
                {
                    LblStatus.Text = "Setting up autounattend.xml...";

                    // Create Unattend folder in temp directory
                    var unattendFolder = System.IO.Path.Combine(tempDir, "Unattend");
                    System.IO.Directory.CreateDirectory(unattendFolder);

                    // Open file dialog to select autounattend.xml
                    var openFileDialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                        Title = "Select autounattend.xml file"
                    };

                    var result = openFileDialog.ShowDialog();

                    if (result == true)
                    {
                        var sourceXmlPath = openFileDialog.FileName;
                        var targetXmlPath = System.IO.Path.Combine(unattendFolder, "autounattend.xml");

                        try
                        {
                            System.IO.File.Copy(sourceXmlPath, targetXmlPath, true);
                            LblStatus.Text = "autounattend.xml copied to Unattend folder";
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"Failed to copy autounattend.xml: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            ChkAddAutounattend.IsChecked = false;
                        }
                    }
                    else
                    {
                        // User cancelled - uncheck the box
                        ChkAddAutounattend.IsChecked = false;
                    }
                }

                // Show app selection dialog only if Custom List is enabled
                if (ChkCustomList.IsChecked == true)
                {
                    if (System.IO.File.Exists(targetCustomAppsList))
                    {
                        var selectedApps = ShowAppSelectionDialog(targetCustomAppsList);
                        if (selectedApps == null)
                        {
                            // User cancelled - abort the download/creation process
                            LblStatus.Text = "Download cancelled";
                            LblStatus.Foreground = Brushes.OrangeRed;
                            MessageBox.Show("Download cancelled by user.", "Cancelled",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            
                            // Cleanup temp folder
                            try
                            {
                                System.IO.Directory.Delete(tempDir, true);
                            }
                            catch { /* Ignore cleanup errors */ }
                            
                            BtnDownload.IsEnabled = true;
                            return;
                        }
                        if (selectedApps.Count > 0)
                        {
                            UpdateCustomAppsList(targetCustomAppsList, selectedApps);
                        }
                    }
                }

                // Update ConvertConfig.ini
                var targetConfig = System.IO.Path.Combine(tempDir, "ConvertConfig.ini");
                if (System.IO.File.Exists(targetConfig))
                {
                    UpdateConvertConfig(targetConfig);
                }

                LblStatus.Text = "Creating ISO (this may take a while)...";
                LblStatus.Foreground = Brushes.LightGreen;

                MessageBox.Show(
                    $"Download complete!\n\nFolder: {tempDir}\n\nThe conversion will start automatically in 3 seconds...",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // Wait 5 seconds and start the conversion script
                await Task.Delay(3000);

                var converterScript = System.IO.Path.Combine(tempDir, "uup_download_windows.cmd");
                if (System.IO.File.Exists(converterScript))
                {
                    LblStatus.Text = "Converting to ISO (this may take a while)...";

                    // Run the converter and wait for it to finish
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"uup_download_windows.cmd\"",
                        WorkingDirectory = tempDir,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal
                    };

                    using var process = Process.Start(processInfo);
                    if (process != null)
                    {
                        process.WaitForExit();

                        // Exit code -1073741510 (0xC000013A) means user closed the window manually
                        // Exit code -1073741502 (0xC000013E) also means window closed
                        // Don't throw error if user closed the window manually
                        if (process.ExitCode != 0 && process.ExitCode != -1073741510 && process.ExitCode != -1073741502)
                        {
                            throw new Exception($"Converter failed with exit code {process.ExitCode}");
                        }
                        else if (process.ExitCode == -1073741510 || process.ExitCode == -1073741502)
                        {
                            // User closed the window manually - treat as cancelled
                            LblStatus.Text = "Conversion cancelled by user";
                            LblStatus.Foreground = Brushes.OrangeRed;
                            MessageBox.Show(
                                "ISO conversion was cancelled by closing the conversion window.\n\n" +
                                $"Temporary files are located at:\n{tempDir}",
                                "Cancelled",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                            BtnDownload.IsEnabled = true;
                            return;
                        }
                    }

                    // Find and move the ISO
                    var isoFiles = System.IO.Directory.GetFiles(tempDir, "*.iso");
                    if (isoFiles.Length > 0)
                    {
                        // Extract build number from build.BuildNumber (e.g., "26200.8106")
                        var buildNumber = build.BuildNumber;

                        // Extract Windows version (10 or 11) from build title
                        var windowsVersion = build.Title.Contains("Windows 11", StringComparison.OrdinalIgnoreCase) ? "11" :
                                            build.Title.Contains("Windows 10", StringComparison.OrdinalIgnoreCase) ? "10" : "11";

                        // Extract edition name (remove special characters and "Windows" prefix)
                        var editionName = edition.Name
                            .Replace("Windows", string.Empty, StringComparison.OrdinalIgnoreCase)
                            .Trim()
                            .Replace(" ", "_")
                            .Trim('_');
                        editionName = new string(editionName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

                        // Extract feature update version (e.g., "25H2") from build title
                        var featureVersion = "Unknown";
                        var h2Match = Regex.Match(build.Title, @"\d+H\d+", RegexOptions.IgnoreCase);
                        if (h2Match.Success)
                        {
                            featureVersion = h2Match.Value.ToUpper();
                        }
                        else
                        {
                            // Try to extract from edition or other patterns
                            var versionMatch = Regex.Match(build.Title, @"version\s*(\d+H\d+)", RegexOptions.IgnoreCase);
                            if (versionMatch.Success)
                            {
                                featureVersion = versionMatch.Groups[1].Value.ToUpper();
                            }
                        }

                        // Extract language code (e.g., "fr-fr", "en-us")
                        var languageCode = language.Code.ToLower();

                        // Build the ISO name: Windows11_Pro_25H2_26200.8106_fr-fr.iso
                        var finalIsoName = $"Windows{windowsVersion}_{editionName}_{featureVersion}_{buildNumber}_{languageCode}.iso";
                        var finalIsoPath = System.IO.Path.Combine(destination, finalIsoName);

                        foreach (var isoFile in isoFiles)
                        {
                            System.IO.File.Move(isoFile, finalIsoPath, true);
                        }

                        LblStatus.Text = "ISO created successfully!";
                        LblStatus.Foreground = Brushes.LightGreen;

                        var isoSize = new System.IO.FileInfo(finalIsoPath).Length;
                        var isoSizeGb = Math.Round(isoSize / (1024.0 * 1024.0 * 1024.0), 2);

                        MessageBox.Show(
                            $"Download and conversion completed successfully!\n\nISO saved to:\n{finalIsoPath}\n\nSize: {isoSizeGb} GB",
                            "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                        // Cleanup temp folder
                        try
                        {
                            System.IO.Directory.Delete(tempDir, true);
                        }
                        catch { /* Ignore cleanup errors */ }
                    }
                    else
                    {
                        throw new Exception($"No ISO file was created. Check the logs in: {tempDir}");
                    }
                }
            }
            catch (Exception ex)
            {
                LblStatus.Text = "Download failed";
                LblStatus.Foreground = Brushes.OrangeRed;
                MessageBox.Show($"Download failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnDownload.IsEnabled = true;
            }
        }

        private List<string>? ShowAppSelectionDialog(string customAppsListPath)
        {
            var dialog = new AppSelectionDialog(this, customAppsListPath);
            var result = dialog.ShowDialog();

            if (result == true)
            {
                return dialog.GetSelectedApps();
            }

            return null; // User cancelled
        }

        private void UpdateCustomAppsList(string path, List<string> selectedApps)
        {
            var lines = System.IO.File.ReadAllLines(path);
            var newLines = new List<string>();
            var inClientSection = false;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    newLines.Add(line);
                    continue;
                }

                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("###") && trimmedLine.Contains("Client"))
                {
                    inClientSection = true;
                    newLines.Add(line);
                    continue;
                }
                if (trimmedLine.StartsWith("###") && !trimmedLine.Contains("Client"))
                {
                    inClientSection = false;
                    newLines.Add(line);
                    continue;
                }

                if (inClientSection)
                {
                    var isCommented = trimmedLine.StartsWith("#");
                    var appName = isCommented ? trimmedLine.Substring(1).Trim() : trimmedLine;

                    if (appName.Contains(".") && appName.Contains("_") &&
                        System.Text.RegularExpressions.Regex.IsMatch(appName, @"^[A-Za-z0-9_.]+$"))
                    {
                        if (selectedApps.Contains(appName))
                        {
                            // Enable: remove #
                            newLines.Add(line.TrimStart('#').TrimStart());
                        }
                        else
                        {
                            // Disable: add # if not present
                            newLines.Add(isCommented ? line : "#" + line);
                        }
                        continue;
                    }
                }

                newLines.Add(line);
            }

            System.IO.File.WriteAllLines(path, newLines);
        }

        private void UpdateConvertConfig(string path)
        {
            var content = System.IO.File.ReadAllText(path);

            void UpdateSetting(string section, string key, string value)
            {
                var pattern = $@"(?m)^\[{section}\][^\[]*";
                var match = System.Text.RegularExpressions.Regex.Match(content, pattern);
                if (match.Success)
                {
                    var sectionContent = match.Value;
                    var keyPattern = $@"(?m)^{key}\s*=.*";
                    if (System.Text.RegularExpressions.Regex.IsMatch(sectionContent, keyPattern))
                    {
                        content = System.Text.RegularExpressions.Regex.Replace(content, keyPattern, $"{key}={value}", System.Text.RegularExpressions.RegexOptions.Multiline);
                    }
                    else
                    {
                        content = System.Text.RegularExpressions.Regex.Replace(content, $@"(?m)^\[{section}\]", $"[{section}]\n{key}={value}");
                    }
                }
                else
                {
                    content += $"\n[{section}]\n{key}={value}";
                }
            }

            // [convert-UUP] section
            if (ChkAutoStart.IsChecked == true) UpdateSetting("convert-UUP", "AutoStart", "1");
            if (ChkUpdates.IsChecked == true) UpdateSetting("convert-UUP", "AddUpdates", "1");
            if (ChkCleanup.IsChecked == true) UpdateSetting("convert-UUP", "Cleanup", "1");
            if (ChkResetBase.IsChecked == true) UpdateSetting("convert-UUP", "ResetBase", "1");
            if (ChkNetFx3.IsChecked == true) UpdateSetting("convert-UUP", "NetFx3", "1");
            if (ChkStartVirtual.IsChecked == true) UpdateSetting("convert-UUP", "StartVirtual", "1");
            if (ChkSolid.IsChecked == true) UpdateSetting("convert-UUP", "wim2esd", "1");
            if (ChkWim2Swm.IsChecked == true) UpdateSetting("convert-UUP", "wim2swm", "1");
            if (ChkSkipISO.IsChecked == true) UpdateSetting("convert-UUP", "SkipISO", "1");
            if (ChkSkipWinRE.IsChecked == true) UpdateSetting("convert-UUP", "SkipWinRE", "1");
            if (ChkLCUwinre.IsChecked == true) UpdateSetting("convert-UUP", "LCUwinre", "1");
            if (ChkUpdtBootFiles.IsChecked == true) UpdateSetting("convert-UUP", "UpdtBootFiles", "1");
            if (ChkForceDism.IsChecked == true) UpdateSetting("convert-UUP", "ForceDism", "1");
            if (ChkRefESD.IsChecked == true) UpdateSetting("convert-UUP", "RefESD", "1");
            if (ChkSkipLCUmsu.IsChecked == true) UpdateSetting("convert-UUP", "SkipLCUmsu", "1");
            if (ChkSkipEdge.IsChecked == true) UpdateSetting("convert-UUP", "SkipEdge", "1");
            if (ChkAutoExit.IsChecked == true) UpdateSetting("convert-UUP", "AutoExit", "1");
            if (ChkDisableUpdatingUpgrade.IsChecked == true) UpdateSetting("convert-UUP", "DisableUpdatingUpgrade", "1");
            if (ChkAddDrivers.IsChecked == true)
            {
                UpdateSetting("convert-UUP", "AddDrivers", "1");
                UpdateSetting("convert-UUP", "Drv_Source", "Drivers");
            }

            // [Store_Apps] section
            if (ChkSkipApps.IsChecked == true) UpdateSetting("Store_Apps", "SkipApps", "1");
            UpdateSetting("Store_Apps", "AppsLevel", CmbAppsLevel.SelectedIndex.ToString());
            if (ChkStubAppsFull.IsChecked == true) UpdateSetting("Store_Apps", "StubAppsFull", "1");
            if (ChkCustomList.IsChecked == true) UpdateSetting("Store_Apps", "CustomList", "1");

            // [create_virtual_editions] section
            if (ChkVUseDism.IsChecked == true) UpdateSetting("create_virtual_editions", "vUseDism", "1");
            if (ChkVAutoStart.IsChecked == true) UpdateSetting("create_virtual_editions", "vAutoStart", "1");
            if (ChkVDeleteSource.IsChecked == true) UpdateSetting("create_virtual_editions", "vDeleteSource", "1");
            if (ChkVPreserve.IsChecked == true) UpdateSetting("create_virtual_editions", "vPreserve", "1");
            if (ChkVwim2esd.IsChecked == true) UpdateSetting("create_virtual_editions", "vwim2esd", "1");
            if (ChkVwim2swm.IsChecked == true) UpdateSetting("create_virtual_editions", "vwim2swm", "1");
            if (ChkVSkipISO.IsChecked == true) UpdateSetting("create_virtual_editions", "vSkipISO", "1");

            System.IO.File.WriteAllText(path, content);
        }

        /// <summary>
        /// Fetches the latest Important_Files.zip download URL from GitHub
        /// </summary>
        private async Task<string?> GetLatestImportantFilesUrlAsync()
        {
            try
            {
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                    var content = await httpClient.GetStringAsync(UUPConfigUrl);
                    
                    // Parse the file to find the Google Drive download URL
                    var lines = content.Split('\n');
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        // Look for Google Drive download links
                        if (trimmedLine.Contains("drive.usercontent.google.com/download") || 
                            trimmedLine.Contains("drive.google.com"))
                        {
                            // Extract URL (may be surrounded by quotes or other characters)
                            var urlStart = trimmedLine.IndexOf("http");
                            if (urlStart >= 0)
                            {
                                var urlEnd = trimmedLine.IndexOfAny(new[] { '"', '\'', ' ', '>' }, urlStart);
                                if (urlEnd < 0) urlEnd = trimmedLine.Length;
                                return trimmedLine.Substring(urlStart, urlEnd - urlStart).Trim();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to fetch Important_Files URL: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// Checks if Important_Files.zip exists and downloads it if needed at startup
        /// Always re-download to ensure latest files
        /// </summary>
        private async Task CheckAndDownloadImportantFilesAsync()
        {
            // Use Windows Temp folder instead of application directory
            var importantFilesZipPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LexBoosT_UUPDumpWPF", "Important_Files.zip");
            var extractFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LexBoosT_UUPDumpWPF", "Important_Files");

            // Always clean up old files before downloading
            try
            {
                if (System.IO.Directory.Exists(extractFolder))
                {
                    System.IO.Directory.Delete(extractFolder, true);
                }
                if (System.IO.File.Exists(importantFilesZipPath))
                {
                    System.IO.File.Delete(importantFilesZipPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clean old files: {ex.Message}");
                // Continue anyway - download will overwrite
            }

            // Ensure directory exists
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(importantFilesZipPath)!);

            try
            {
                // Show download overlay
                DownloadOverlay.Visibility = Visibility.Visible;
                DownloadProgress.Value = 0;
                DownloadStatus.Text = "Cleaning old files...";
                LblStatus.Text = "Refreshing necessary files...";

                // Get the download URL from GitHub
                var downloadUrl = await GetLatestImportantFilesUrlAsync();

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    DownloadOverlay.Visibility = Visibility.Collapsed;
                    LblStatus.Text = "Failed to get download URL";
                    MessageBox.Show(
                        "Unable to retrieve necessary files. Please check your internet connection.\n\n" +
                        "The application will continue, but you won't be able to create ISOs until the files are available.",
                        "Download Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Download the file with progress
                DownloadStatus.Text = "Connecting to server...";
                await DownloadImportantFilesAsync(importantFilesZipPath, downloadUrl, true);

                // Extract the downloaded ZIP file
                DownloadStatus.Text = "Extracting files...";
                System.IO.Directory.CreateDirectory(extractFolder);
                ZipFile.ExtractToDirectory(importantFilesZipPath, extractFolder, true);

                // Hide overlay on success
                DownloadOverlay.Visibility = Visibility.Collapsed;
                LblStatus.Text = "Files downloaded and extracted";

                MessageBox.Show(
                    "Necessary files downloaded successfully!\n\n" +
                    "You can now start creating ISOs.",
                    "Download Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DownloadOverlay.Visibility = Visibility.Collapsed;
                LblStatus.Text = "Download failed";
                MessageBox.Show(
                    $"Failed to download necessary files: {ex.Message}\n\n" +
                    "The application will continue, but you won't be able to create ISOs until the files are available.",
                    "Download Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Downloads necessary files from the specified URL
        /// </summary>
        private async Task DownloadImportantFilesAsync(string destinationPath, string? downloadUrl = null, bool showProgress = false)
        {
            // If no URL provided, fetch the latest from GitHub
            if (string.IsNullOrEmpty(downloadUrl))
            {
                downloadUrl = await GetLatestImportantFilesUrlAsync();
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                throw new Exception("Unable to retrieve download URL for necessary files. Please check your internet connection.");
            }

            using (var httpClient = new System.Net.Http.HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                httpClient.Timeout = TimeSpan.FromMinutes(10);

                // Get the file size for progress reporting
                var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength;
                var isProgressSupported = totalBytes.HasValue && showProgress;

                // Ensure directory exists
                var directory = System.IO.Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                // Download with progress reporting
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new System.IO.FileStream(destinationPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    long totalBytesRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        totalBytesRead += bytesRead;

                        if (isProgressSupported)
                        {
                            var progress = (double)totalBytesRead / totalBytes!.Value * 100;
                            
                            // Update UI on the dispatcher thread
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                DownloadProgress.Value = progress;
                                DownloadStatus.Text = $"{FormatFileSize(totalBytesRead)} / {FormatFileSize(totalBytes.Value)} ({progress:F1}%)";
                            });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Formats a file size in bytes to a human-readable string
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Copies a directory recursively
        /// </summary>
        private void CopyDirectory(string sourceDir, string targetDir, bool overwrite)
        {
            // Create target directory if it doesn't exist
            System.IO.Directory.CreateDirectory(targetDir);

            // Copy all files
            foreach (var file in System.IO.Directory.GetFiles(sourceDir))
            {
                var targetFile = System.IO.Path.Combine(targetDir, System.IO.Path.GetFileName(file));
                System.IO.File.Copy(file, targetFile, overwrite);
            }

            // Copy all subdirectories recursively
            foreach (var directory in System.IO.Directory.GetDirectories(sourceDir))
            {
                var targetSubDir = System.IO.Path.Combine(targetDir, System.IO.Path.GetFileName(directory));
                CopyDirectory(directory, targetSubDir, overwrite);
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // Double click to maximize/restore
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // Open the URL in the default browser
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}
