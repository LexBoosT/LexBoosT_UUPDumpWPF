using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        private List<Build> _currentBuilds = new();
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
                LblCurrentVersionInfo.Text = $"{version.FullVersion} - Edition: {version.Edition}";
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
                if (!int.TryParse(currentBuildParts[0], out int currentMajor))
                    return;

                // Search for newer builds on UUP Dump (fast - only top 50)
                var builds = await _uupService.GetBuildsAsync("windows 11");
                
                if (builds.Count == 0)
                    return;

                // Find the highest build number
                var highestBuild = builds.FirstOrDefault();
                if (highestBuild == null)
                    return;

                var highestBuildParts = highestBuild.BuildNumber.Split('.');
                if (!int.TryParse(highestBuildParts[0], out int highestMajor))
                    return;

                // Compare builds (only for same major version)
                if (highestMajor == currentMajor && highestBuild.BuildNumber != currentVersion.Build)
                {
                    double currentBuildNum = currentBuildParts.Length >= 2 && 
                        int.TryParse(currentBuildParts[1], out int currentMinor) 
                        ? currentMajor + currentMinor / 10000.0 
                        : currentMajor;

                    double highestBuildNum = highestBuildParts.Length >= 2 && 
                        int.TryParse(highestBuildParts[1], out int highestMinor) 
                        ? highestMajor + highestMinor / 10000.0 
                        : highestMajor;

                    if (highestBuildNum > currentBuildNum)
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
                
                // Apply filters
                var filteredBuilds = builds.Where(b =>
                {
                    var retailMatch = (ChkRetail.IsChecked == true && b.IsRetail) ||
                                     (ChkPreview.IsChecked == true && !b.IsRetail);
                    var archMatch = (ChkAmd64.IsChecked == true && b.Architecture == "amd64") ||
                                   (ChkArm64.IsChecked == true && b.Architecture == "arm64");
                    return retailMatch && archMatch;
                }).ToList();

                _currentBuilds = filteredBuilds;
                LstBuilds.ItemsSource = filteredBuilds.Select(b => 
                    $"{b.Title} ({b.BuildNumber}) - {b.Architecture}");
                
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
                LblStatus.Text = "Ready to download";
                LblStatus.Foreground = Brushes.LightGreen;
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder to save ISO files",
                SelectedPath = TxtDestination.Text,
                ShowNewFolderButton = true
            };

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtDestination.Text = folderDialog.SelectedPath;
            }
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

        private async Task StartDownloadAsync(Build build, Language language, Edition edition)
        {
            var destination = TxtDestination.Text;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var buildTitle = new string(build.Title.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
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
                    $"Download complete!\n\nFolder: {tempDir}\n\nThe conversion will start automatically in 5 seconds...",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // Wait 5 seconds and start the conversion script
                await Task.Delay(5000);

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
                        var safeBuildTitle = new string(build.Title.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
                        var finalIsoName = $"Windows_{safeBuildTitle}_{edition.Code}_{language.Code}_{timestamp}.iso";
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
            var apps = new List<(string Code, string Name, string FullName, bool Selected)>();
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
                        var appCode = appName.Split('_')[0];
                        apps.Add((appCode, appName, appName, !isCommented));
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
    }
}
