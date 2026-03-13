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
using Microsoft.Win32;
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

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private readonly UUPDumpService _uupService;
        private readonly WindowsVersionService _versionService;

        private List<Build> _allBuilds = new(); // All builds from search
        private List<Build> _currentBuilds = new(); // Filtered builds
        private List<Language> _currentLanguages = new();
        private List<Edition> _currentEditions = new();
        private Build? _selectedBuild;

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
                LstEditions.ItemsSource = _currentEditions.Select(e => $"{e.Name} ({e.Code})");
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
            if (LstEditions.SelectedIndex >= 0)
            {
                LblStatus.Text = "Ready to make ISO or Download Update";
                LblStatus.Foreground = Brushes.LightGreen;
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
            if (ChkUpdates.IsChecked == true) optionsInfo += "Updates, ";
            if (ChkCleanup.IsChecked == true) optionsInfo += "Cleanup, ";
            if (ChkNetFx3.IsChecked == true) optionsInfo += ".NET 3.5, ";
            if (ChkSolid.IsChecked == true) optionsInfo += "ESD, ";
            if (ChkSkipEdge.IsChecked == true) optionsInfo += "Skip Edge, ";
            optionsInfo = optionsInfo.TrimEnd(',', ' ');
            if (string.IsNullOrEmpty(optionsInfo)) optionsInfo = "Default options";

            var message = $"Build: {_selectedBuild.Title}\n";
            message += $"Language: {selectedLanguage.Name}\n";
            message += $"Edition: {selectedEdition.Name}\n";
            message += $"Options: {optionsInfo}\n";
            message += $"Destination: {TxtDestination.Text}\n\n";
            message += "This will download and create the ISO automatically.\nThis may take a while, depending on your connection.\n\nContinue?";

            var result = MessageBox.Show(message, "Confirmation",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            // Start download process
            await StartDownloadAsync(_selectedBuild, selectedLanguage, selectedEdition);
        }

        private async Task StartDownloadPackAsync(Build build, Language language, Edition edition)
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

                LblStatus.Text = "Downloading package...";
                LblStatus.Foreground = Brushes.SkyBlue;
                BtnDownloadPack.IsEnabled = false;

                // Build download URL for update pack only
                // Must use POST request with autodl=1 parameter
                var downloadUrl = $"https://uupdump.net/get.php?id={build.Id}&pack=0&edition=updateOnly";

                var zipPath = System.IO.Path.Combine(tempDir, "package.zip");

                // Download with HttpClient using POST
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                    httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

                    // POST with autodl=1 parameter (same as clicking "Create download package for these updates")
                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("autodl", "1")
                    });

                    var response = await httpClient.PostAsync(downloadUrl, content);
                    response.EnsureSuccessStatusCode();

                    // Check if we got redirected or received HTML instead of zip
                    var contentType = response.Content.Headers.ContentType?.MediaType;
                    if (contentType == "text/html")
                    {
                        throw new Exception($"Server returned HTML instead of ZIP. The update pack may not be available for this build.\n\nURL: {downloadUrl}");
                    }

                    using (var fs = new System.IO.FileStream(zipPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                // Verify the file is a valid ZIP
                var fileInfo = new System.IO.FileInfo(zipPath);
                if (!fileInfo.Exists || fileInfo.Length < 1000)
                {
                    throw new Exception("Download failed - file is missing or too small");
                }

                // Check ZIP magic number
                using (var zipStream = new System.IO.FileStream(zipPath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    if (zipStream.Length < 4)
                    {
                        throw new Exception("Downloaded file is not a valid ZIP archive");
                    }
                    var header = new byte[4];
                    zipStream.Read(header, 0, 4);
                    // ZIP files start with PK\x03\x04 (50 4B 03 04)
                    if (header[0] != 0x50 || header[1] != 0x4B || header[2] != 0x03 || header[3] != 0x04)
                    {
                        throw new Exception("Downloaded file is not a valid ZIP archive (invalid header). The update pack may not be available for this build.");
                    }
                }

                LblStatus.Text = "Extracting package...";

                // Extract zip
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir, true);
                System.IO.File.Delete(zipPath);

                LblStatus.Text = "Processing configuration...";

                // Show app selection dialog if CustomAppsList.txt exists
                var customAppsListPath = System.IO.Path.Combine(tempDir, "CustomAppsList.txt");
                if (System.IO.File.Exists(customAppsListPath))
                {
                    var selectedApps = ShowAppSelectionDialog(customAppsListPath);
                    if (selectedApps != null)
                    {
                        UpdateCustomAppsList(customAppsListPath, selectedApps);
                    }
                }

                // Update ConvertConfig.ini
                var targetConfig = System.IO.Path.Combine(tempDir, "ConvertConfig.ini");
                if (System.IO.File.Exists(targetConfig))
                {
                    UpdateConvertConfig(targetConfig);
                }

                LblStatus.Text = "Running uup_download_windows.cmd...";
                LblStatus.Foreground = Brushes.LightGreen;

                // Run uup_download_windows.cmd
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

                        if (process.ExitCode != 0)
                        {
                            throw new Exception($"Download script failed with exit code {process.ExitCode}");
                        }
                    }
                }

                // Wait for UUPs folder to be created
                LblStatus.Text = "Waiting for UUPs folder...";
                var uupsFolder = System.IO.Path.Combine(tempDir, "UUPs");
                var maxWaitTime = TimeSpan.FromMinutes(30);
                var waitInterval = TimeSpan.FromSeconds(2);
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
                await Task.Delay(2000);

                LblStatus.Text = "Downloading W10UI from GitHub...";

                // Download W10UI files from GitHub repository in parallel
                var win10uiFolder = System.IO.Path.Combine(tempDir, "W10UI");
                if (!System.IO.Directory.Exists(win10uiFolder))
                {
                    System.IO.Directory.CreateDirectory(win10uiFolder);
                }

                // Download W10UI files from GitHub repository
                var w10uiFiles = new[]
                {
                    "W10UI.cmd", "W10UI.ini"
                };

                var githubBaseUrl = "https://raw.githubusercontent.com/abbodi1406/BatUtil/master/W10UI/";

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                    // Download all files in parallel
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

                    // If no files downloaded, try downloading the main W10UI.cmd only
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

                    LblStatus.Text = $"W10UI files downloaded ({downloadedCount} files). Copying to UUPs...";
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
                    // Try to find it in subdirectories
                    var w10uiFilesFound = System.IO.Directory.GetFiles(uupsFolder, "W10UI.cmd", System.IO.SearchOption.AllDirectories);
                    w10uiScript = w10uiFilesFound.FirstOrDefault() ?? null;
                }

                if (System.IO.File.Exists(w10uiScript))
                {
                    LblStatus.Text = "Ready to install update";
                    LblStatus.Foreground = Brushes.LightGreen;

                    // Ask user if they want to install the update
                    var result = MessageBox.Show(
                        $"W10UI files are ready.\n\nFolder: {tempDir}\n\nDo you want to launch W10UI.cmd to install the update?\n\nClick 'Yes' to launch W10UI.cmd\nClick 'No' to open the folder and exit",
                        "Install Update",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        LblStatus.Text = "Launching W10UI.cmd...";

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
                        // Open folder and exit
                        System.Diagnostics.Process.Start("explorer.exe", tempDir);
                        Application.Current.Shutdown();
                    }
                }
                else
                {
                    throw new Exception("W10UI.cmd not found in UUPs folder");
                }
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

                LblStatus.Text = "Downloading package...";
                LblStatus.Foreground = Brushes.SkyBlue;
                BtnDownload.IsEnabled = false;

                // Build download URL
                var downloadUrl = $"https://uupdump.net/get.php?id={build.Id}&pack={language.Code}&edition={edition.Code}&autodl=2&updates={(ChkUpdates.IsChecked == true ? 1 : 0)}&cleanup={(ChkCleanup.IsChecked == true ? 1 : 0)}";
                if (ChkNetFx3.IsChecked == true) downloadUrl += "&netfx3=1";
                if (ChkSolid.IsChecked == true) downloadUrl += "&esd=1";
                if (ChkSkipEdge.IsChecked == true) downloadUrl += "&skipedge=1";

                var zipPath = System.IO.Path.Combine(tempDir, "package.zip");

                // Download with HttpClient
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                    httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

                    var response = await httpClient.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();

                    using (var fs = new System.IO.FileStream(zipPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                if (!System.IO.File.Exists(zipPath) || new System.IO.FileInfo(zipPath).Length < 1000)
                {
                    throw new Exception("Download failed - file is missing or too small");
                }

                LblStatus.Text = "Extracting package...";

                // Extract zip
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir, true);
                System.IO.File.Delete(zipPath);

                LblStatus.Text = "Processing configuration...";

                // Show app selection dialog if CustomAppsList.txt exists
                var customAppsListPath = System.IO.Path.Combine(tempDir, "CustomAppsList.txt");
                if (System.IO.File.Exists(customAppsListPath))
                {
                    var selectedApps = ShowAppSelectionDialog(customAppsListPath);
                    if (selectedApps != null)
                    {
                        UpdateCustomAppsList(customAppsListPath, selectedApps);
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

                        if (process.ExitCode != 0)
                        {
                            throw new Exception($"Converter failed with exit code {process.ExitCode}");
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

        private List<string> ShowAppSelectionDialog(string customAppsListPath)
        {
            var apps = new List<(string Name, string FullName, bool Selected)>();
            var inClientSection = false;

            var lines = System.IO.File.ReadAllLines(customAppsListPath);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.Trim().StartsWith("###") && line.Contains("Client"))
                {
                    inClientSection = true;
                    continue;
                }
                if (line.Trim().StartsWith("###") && !line.Contains("Client"))
                {
                    if (inClientSection) break;
                    continue;
                }

                if (inClientSection)
                {
                    var trimmedLine = line.Trim();
                    var isCommented = trimmedLine.StartsWith("#");

                    var appName = isCommented ? trimmedLine.Substring(1).Trim() : trimmedLine;

                    if (appName.Contains(".") && appName.Contains("_") &&
                        System.Text.RegularExpressions.Regex.IsMatch(appName, @"^[A-Za-z0-9_.]+$"))
                    {
                        apps.Add((Name: appName, FullName: appName, Selected: !isCommented));
                    }
                }
            }

            if (apps.Count == 0) return new List<string>();

            // Create dialog
            var dialog = new Window
            {
                Title = "Select Applications to Install",
                Width = 500,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = this
            };

            var mainGrid = new Grid { Margin = new Thickness(10) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Instruction
            var lblInstruction = new TextBlock
            {
                Text = "Select the applications you want to integrate:",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(lblInstruction, 0);

            // Apps list
            var lstApps = new System.Windows.Controls.ListBox
            {
                Margin = new Thickness(0, 0, 0, 10)
            };

            var appCheckboxes = new List<CheckBox>();
            foreach (var app in apps)
            {
                var cb = new CheckBox
                {
                    Content = app.Name.Length > 50 ? app.Name.Substring(0, 47) + "..." : app.Name,
                    IsChecked = app.Selected,
                    Margin = new Thickness(0, 2, 0, 2),
                    Tag = app.FullName
                };
                appCheckboxes.Add(cb);
                lstApps.Items.Add(cb);
            }

            Grid.SetRow(lstApps, 1);

            // Buttons panel
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var btnSelectAll = new Button { Content = "Select All", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
            btnSelectAll.Click += (s, e) => appCheckboxes.ForEach(cb => cb.IsChecked = true);

            var btnSelectNone = new Button { Content = "Select None", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
            btnSelectNone.Click += (s, e) => appCheckboxes.ForEach(cb => cb.IsChecked = false);

            var btnOK = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 10, 0), IsDefault = true };
            btnOK.Click += (s, e) => dialog.DialogResult = true;

            var btnCancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
            btnCancel.Click += (s, e) => dialog.DialogResult = false;

            btnPanel.Children.Add(btnSelectAll);
            btnPanel.Children.Add(btnSelectNone);
            btnPanel.Children.Add(btnOK);
            btnPanel.Children.Add(btnCancel);

            Grid.SetRow(btnPanel, 2);

            mainGrid.Children.Add(lblInstruction);
            mainGrid.Children.Add(lstApps);
            mainGrid.Children.Add(btnPanel);

            dialog.Content = mainGrid;

            var result = dialog.ShowDialog();

            if (result == true)
            {
                return appCheckboxes.Where(cb => cb.IsChecked == true).Select(cb => cb.Tag!.ToString()!).ToList();
            }

            return null!;
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

            // Enable CustomList
            if (System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^CustomList\s*=\s*0"))
            {
                content = System.Text.RegularExpressions.Regex.Replace(content, @"(?m)^CustomList\s*=\s*0", "CustomList=1");
            }
            else if (!System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^CustomList\s*="))
            {
                content = System.Text.RegularExpressions.Regex.Replace(content, @"(?m)^(\[Settings\])", "$1\nCustomList=1");
            }

            // Include Updates -> AddUpdates=1
            if (ChkUpdates.IsChecked == true)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^AddUpdates\s*=\s*0"))
                {
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"(?m)^AddUpdates\s*=\s*0", "AddUpdates=1");
                }
                else if (!System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^AddUpdates\s*="))
                {
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"(?m)^(\[Settings\])", "$1\nAddUpdates=1");
                }
            }

            // Cleanup -> Cleanup=1
            if (ChkCleanup.IsChecked == true)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^Cleanup\s*=\s*0"))
                {
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"(?m)^Cleanup\s*=\s*0", "Cleanup=1");
                }
                else if (!System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^Cleanup\s*="))
                {
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"(?m)^(\[Settings\])", "$1\nCleanup=1");
                }
            }

            // .NET 3.5 -> NetFx3=1
            if (ChkNetFx3.IsChecked == true)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^NetFx3\s*=\s*0"))
                {
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"(?m)^NetFx3\s*=\s*0", "NetFx3=1");
                }
                else if (!System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^NetFx3\s*="))
                {
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"(?m)^(\[Settings\])", "$1\nNetFx3=1");
                }
            }

            // ESD Compression -> wim2esd=1
            if (ChkSolid.IsChecked == true)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^wim2esd\s*=\s*0"))
                {
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"(?m)^wim2esd\s*=\s*0", "wim2esd=1");
                }
                else if (!System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^wim2esd\s*="))
                {
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"(?m)^(\[ConvertConfig\])", "$1\nwim2esd=1");
                }
            }

            // Skip Edge -> SkipEdge=1
            if (ChkSkipEdge.IsChecked == true)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^SkipEdge\s*=\s*0"))
                {
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"(?m)^SkipEdge\s*=\s*0", "SkipEdge=1");
                }
                else if (!System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^SkipEdge\s*="))
                {
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"(?m)^(\[Settings\])", "$1\nSkipEdge=1");
                }
            }

            System.IO.File.WriteAllText(path, content);
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
