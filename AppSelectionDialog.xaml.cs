using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace UUPDumpWPF
{
    public partial class AppSelectionDialog : Window
    {
        private readonly string _customAppsListPath;
        private readonly List<AppItem> _appItems = new();

        // Friendly names mapping for common apps - based on CustomAppsList.txt
        private static readonly Dictionary<string, string> FriendlyNames = new()
        {
            // ### Common Apps / Client editions all
            { "Microsoft.WindowsStore_8wekyb3d8bbwe", "Microsoft Store (Main Store App)" },
            { "Microsoft.StorePurchaseApp_8wekyb3d8bbwe", "Microsoft Store (Purchase Helper - Licenses)" },
            { "Microsoft.SecHealthUI_8wekyb3d8bbwe", "Security Health (Windows Security)" },
            { "Microsoft.DesktopAppInstaller_8wekyb3d8bbwe", "App Installer (MSIX/Appx Installer)" },
            { "Microsoft.Windows.Photos_8wekyb3d8bbwe", "Photos (Photo Viewer & Editor)" },
            { "Microsoft.WindowsCamera_8wekyb3d8bbwe", "Camera (Webcam App)" },
            { "Microsoft.WindowsNotepad_8wekyb3d8bbwe", "Notepad (Text Editor)" },
            { "Microsoft.Paint_8wekyb3d8bbwe", "Paint (Drawing App)" },
            { "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "Windows Terminal (Command Line)" },
            { "MicrosoftWindows.Client.WebExperience_cw5n1h2txyewy", "Widgets (Taskbar Widgets)" },
            { "Microsoft.WindowsAlarms_8wekyb3d8bbwe", "Alarms & Clock" },
            { "Microsoft.WindowsCalculator_8wekyb3d8bbwe", "Calculator" },
            { "Microsoft.WindowsMaps_8wekyb3d8bbwe", "Maps (GPS & Offline Maps)" },
            { "Microsoft.MicrosoftStickyNotes_8wekyb3d8bbwe", "Sticky Notes (Post-it Notes)" },
            { "Microsoft.ScreenSketch_8wekyb3d8bbwe", "Snipping Tool (Screenshot App)" },
            { "microsoft.windowscommunicationsapps_8wekyb3d8bbwe", "Mail and Calendar (Outlook Lite)" },
            { "Microsoft.People_8wekyb3d8bbwe", "People (Contacts App)" },
            { "Microsoft.BingNews_8wekyb3d8bbwe", "News (Bing News)" },
            { "Microsoft.BingWeather_8wekyb3d8bbwe", "Weather Forecast (Bing)" },
            { "Microsoft.MicrosoftSolitaireCollection_8wekyb3d8bbwe", "Solitaire Card Games" },
            { "Microsoft.MicrosoftOfficeHub_8wekyb3d8bbwe", "Get Office (Office 365 Hub)" },
            { "Microsoft.WindowsFeedbackHub_8wekyb3d8bbwe", "Feedback Hub (Send Feedback)" },
            { "Microsoft.GetHelp_8wekyb3d8bbwe", "Get Help (Windows Support)" },
            { "Microsoft.Getstarted_8wekyb3d8bbwe", "Tips (Windows Getting Started)" },
            { "Microsoft.Todos_8wekyb3d8bbwe", "Microsoft To Do (Tasks List)" },
            { "Microsoft.XboxSpeechToTextOverlay_8wekyb3d8bbwe", "Xbox Voice to Text" },
            { "Microsoft.XboxGameOverlay_8wekyb3d8bbwe", "Xbox Game Bar Overlay" },
            { "Microsoft.XboxIdentityProvider_8wekyb3d8bbwe", "Xbox Identity Provider (Login)" },
            { "Microsoft.PowerAutomateDesktop_8wekyb3d8bbwe", "Power Automate (Workflow Automation)" },
            { "Microsoft.549981C3F5F10_8wekyb3d8bbwe", "Cortana (Voice Assistant)" },
            { "MicrosoftCorporationII.QuickAssist_8wekyb3d8bbwe", "Quick Assist (Remote Help)" },
            { "MicrosoftCorporationII.MicrosoftFamily_8wekyb3d8bbwe", "Family Safety (Parental Controls)" },
            { "Clipchamp.Clipchamp_yxz26nhyzhsrt", "Clipchamp (Video Editor)" },
            { "Microsoft.OutlookForWindows_8wekyb3d8bbwe", "Outlook (Email App)" },
            { "MicrosoftTeams_8wekyb3d8bbwe", "Microsoft Teams (Chat & Meetings)" },
            { "Microsoft.Windows.DevHome_8wekyb3d8bbwe", "Dev Home (Developer Dashboard)" },
            { "Microsoft.BingSearch_8wekyb3d8bbwe", "Bing Search" },
            { "Microsoft.ApplicationCompatibilityEnhancements_8wekyb3d8bbwe", "Application Compatibility Enhancements" },
            { "MicrosoftWindows.CrossDevice_cw5n1h2txyewy", "Cross Device (Phone Link)" },
            { "MSTeams_8wekyb3d8bbwe", "Microsoft Teams (New Version)" },
            { "Microsoft.MicrosoftPCManager_8wekyb3d8bbwe", "Microsoft PC Manager (System Optimizer)" },
            { "Microsoft.StartExperiencesApp_8wekyb3d8bbwe", "Microsoft Start (News & Interests)" },
            { "Microsoft.WidgetsPlatformRuntime_8wekyb3d8bbwe", "Widgets Platform Runtime" },
            { "Microsoft.Copilot_8wekyb3d8bbwe", "Copilot (AI Assistant)" },

            // ### Media Apps / Client non-N editions
            { "Microsoft.ZuneMusic_8wekyb3d8bbwe", "Groove Music (Music Player)" },
            { "Microsoft.ZuneVideo_8wekyb3d8bbwe", "Movies and TV (Video Player)" },
            { "Microsoft.YourPhone_8wekyb3d8bbwe", "Phone Link (Android/iPhone Sync)" },
            { "Microsoft.WindowsSoundRecorder_8wekyb3d8bbwe", "Voice Recorder (Audio Recording)" },
            { "Microsoft.GamingApp_8wekyb3d8bbwe", "Xbox Game Bar (Gaming Overlay)" },
            { "Microsoft.XboxGamingOverlay_8wekyb3d8bbwe", "Xbox Game Bar (Gaming)" },
            { "Microsoft.Xbox.TCUI_8wekyb3d8bbwe", "Xbox Live Chat (TCUI)" },

            // ### Media Codecs / Client non-N editions, Team edition
            { "Microsoft.WebMediaExtensions_8wekyb3d8bbwe", "Web Media Extensions (Web Codecs)" },
            { "Microsoft.RawImageExtension_8wekyb3d8bbwe", "RAW Image Extension (Camera RAW)" },
            { "Microsoft.HEIFImageExtension_8wekyb3d8bbwe", "HEIF Image Extension (Photo Format)" },
            { "Microsoft.HEVCVideoExtension_8wekyb3d8bbwe", "HEVC Video Extension (H.265 Video)" },
            { "Microsoft.VP9VideoExtensions_8wekyb3d8bbwe", "VP9 Video Extension (Web Video)" },
            { "Microsoft.WebpImageExtension_8wekyb3d8bbwe", "WebP Image Extension (WebP Images)" },
            { "Microsoft.DolbyAudioExtensions_8wekyb3d8bbwe", "Dolby Audio Extensions (Surround Sound)" },
            { "Microsoft.AVCEncoderVideoExtension_8wekyb3d8bbwe", "AVC Encoder Video Extension (H.264)" },
            { "Microsoft.MPEG2VideoExtension_8wekyb3d8bbwe", "MPEG-2 Video Extension (DVD Video)" },
            { "Microsoft.AV1VideoExtension_8wekyb3d8bbwe", "AV1 Video Extension (Next-Gen Codec)" },

            // ### Surface Hub Apps / Team edition
            { "Microsoft.Whiteboard_8wekyb3d8bbwe", "Whiteboard (Digital Collaboration)" },
            { "microsoft.microsoftskydrive_8wekyb3d8bbwe", "OneDrive (Cloud Storage)" },
            { "Microsoft.MicrosoftTeamsforSurfaceHub_8wekyb3d8bbwe", "Microsoft Teams for Surface Hub" },
            { "MicrosoftCorporationII.MailforSurfaceHub_8wekyb3d8bbwe", "Mail for Surface Hub" },
            { "Microsoft.MicrosoftPowerBIForWindows_8wekyb3d8bbwe", "Power BI (Business Analytics)" },
            { "Microsoft.SkypeApp_kzf8qxf38zg5c", "Skype for Business" },
            { "Microsoft.Office.Excel_8wekyb3d8bbwe", "Excel (Spreadsheet App)" },
            { "Microsoft.Office.PowerPoint_8wekyb3d8bbwe", "PowerPoint (Presentation App)" },
            { "Microsoft.Office.Word_8wekyb3d8bbwe", "Word (Document Editor)" }
        };

        public AppSelectionDialog(Window owner, string customAppsListPath)
        {
            InitializeComponent();
            Owner = owner;
            _customAppsListPath = customAppsListPath;

            LoadApps();
        }

        private void LoadApps()
        {
            if (!File.Exists(_customAppsListPath))
            {
                MessageBox.Show("CustomAppsList.txt not found!", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                return;
            }

            var lines = File.ReadAllLines(_customAppsListPath);
            var inClientSection = false;

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
                        var displayName = GetFriendlyName(appName);
                        var isSelected = !isCommented;

                        var checkBox = new CheckBox
                        {
                            Content = displayName,
                            IsChecked = isSelected,
                            Margin = new Thickness(0, 5, 0, 5),
                            Foreground = System.Windows.Media.Brushes.White,
                            FontSize = 13,
                            Tag = appName,
                            ToolTip = appName
                        };

                        _appItems.Add(new AppItem { Code = appName, CheckBox = checkBox });
                        AppsPanel.Children.Add(checkBox);
                    }
                }
            }

            if (_appItems.Count == 0)
            {
                MessageBox.Show("No applications found in CustomAppsList.txt!", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = false;
            }
        }

        private string GetFriendlyName(string appCode)
        {
            // First check if we have a friendly name in the dictionary
            if (FriendlyNames.TryGetValue(appCode, out var friendlyName))
            {
                return friendlyName;
            }

            // Try to extract a readable name from package family name format
            // e.g., "Microsoft.WindowsStore_8wekyb3d8bbwe" -> "Microsoft Store"
            // e.g., "Microsoft.Windows.Photos_8wekyb3d8bbwe" -> "Photos"
            
            // Remove package SID suffix (everything after underscore)
            var baseName = appCode.Split('_')[0];
            
            var parts = baseName.Split('.');
            if (parts.Length > 1)
            {
                // Remove "Microsoft" prefix if present
                var nameParts = parts.Skip(parts[0].Equals("Microsoft", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
                var name = string.Join(" ", nameParts);
                
                // Remove common suffixes and clean up
                name = name.Replace("App", "", StringComparison.OrdinalIgnoreCase)
                          .Replace("Application", "", StringComparison.OrdinalIgnoreCase)
                          .Replace("Extension", "", StringComparison.OrdinalIgnoreCase)
                          .Replace("Windows", "", StringComparison.OrdinalIgnoreCase)
                          .Replace("Microsoft", "", StringComparison.OrdinalIgnoreCase)
                          .Replace("Corporation", "", StringComparison.OrdinalIgnoreCase)
                          .Trim();
                
                // Add spaces before capital letters for better readability
                name = System.Text.RegularExpressions.Regex.Replace(name, @"([a-z])([A-Z])", "$1 $2");
                
                // Special cases for common apps - with very explicit names
                if (name.Contains("Store", StringComparison.OrdinalIgnoreCase))
                {
                    // Check if it's the main Store or a related app
                    if (appCode.Contains("WindowsStore", StringComparison.OrdinalIgnoreCase))
                        return "Microsoft Store (Main Store App)";
                    if (appCode.Contains("StorePurchase", StringComparison.OrdinalIgnoreCase))
                        return "Microsoft Store (Purchase Helper - Licenses)";
                    return "Microsoft Store";
                }
                if (name.Contains("Photos", StringComparison.OrdinalIgnoreCase))
                {
                    if (appCode.Contains("Photos", StringComparison.OrdinalIgnoreCase) && appCode.Contains("Legacy", StringComparison.OrdinalIgnoreCase))
                        return "Photo Viewer (Legacy)";
                    return "Photo Viewer (Photos App)";
                }
                if (name.Contains("Calculator", StringComparison.OrdinalIgnoreCase))
                    return "Calculator";
                if (name.Contains("Weather", StringComparison.OrdinalIgnoreCase))
                    return "Weather Forecast (Bing)";
                if (name.Contains("Maps", StringComparison.OrdinalIgnoreCase))
                    return "GPS Maps";
                if (name.Contains("OneNote", StringComparison.OrdinalIgnoreCase))
                {
                    if (appCode.Contains("Desktop", StringComparison.OrdinalIgnoreCase))
                        return "OneNote Desktop";
                    return "OneNote (Notes App)";
                }
                if (name.Contains("Solitaire", StringComparison.OrdinalIgnoreCase))
                    return "Solitaire Card Games";
                if (name.Contains("Sticky", StringComparison.OrdinalIgnoreCase))
                    return "Sticky Notes (Post-it Notes)";
                if (name.Contains("Paint", StringComparison.OrdinalIgnoreCase))
                    return "Paint 3D (Drawing App)";
                if (name.Contains("Camera", StringComparison.OrdinalIgnoreCase))
                    return "Camera (Webcam App)";
                if (name.Contains("Notepad", StringComparison.OrdinalIgnoreCase))
                    return "Notepad (Text Editor)";
                if (name.Contains("Terminal", StringComparison.OrdinalIgnoreCase))
                    return "Windows Terminal (Command Line)";
                if (name.Contains("Teams", StringComparison.OrdinalIgnoreCase))
                {
                    if (appCode.Contains("Skype", StringComparison.OrdinalIgnoreCase))
                        return "Microsoft Teams (Skype Integration)";
                    return "Microsoft Teams (Chat & Meetings)";
                }
                if (name.Contains("Outlook", StringComparison.OrdinalIgnoreCase))
                    return "Outlook (Email App)";
                if (name.Contains("Xbox", StringComparison.OrdinalIgnoreCase))
                {
                    if (appCode.Contains("GamingOverlay", StringComparison.OrdinalIgnoreCase))
                        return "Xbox Game Bar (Gaming Overlay)";
                    if (appCode.Contains("TCUI", StringComparison.OrdinalIgnoreCase))
                        return "Xbox Live Chat (TCUI)";
                    if (appCode.Contains("SpeechToText", StringComparison.OrdinalIgnoreCase))
                        return "Xbox Voice to Text";
                    if (appCode.Contains("GameCallableUI", StringComparison.OrdinalIgnoreCase))
                        return "Xbox Game UI Helper";
                    if (appCode.Contains("XboxApp", StringComparison.OrdinalIgnoreCase))
                        return "Xbox Console Companion (Xbox App)";
                    return name;
                }
                if (name.Contains("Office", StringComparison.OrdinalIgnoreCase))
                {
                    if (appCode.Contains("Hub", StringComparison.OrdinalIgnoreCase))
                        return "Get Office (Office 365 Hub)";
                    return "Office";
                }
                if (name.Contains("Clipchamp", StringComparison.OrdinalIgnoreCase))
                    return "Clipchamp (Video Editor)";
                if (name.Contains("PowerToys", StringComparison.OrdinalIgnoreCase))
                    return "PowerToys (System Utilities)";
                if (name.Contains("Copilot", StringComparison.OrdinalIgnoreCase))
                    return "Copilot (AI Assistant)";
                if (name.Contains("Recall", StringComparison.OrdinalIgnoreCase))
                    return "Recall (Activity History)";
                if (name.Contains("Cortana", StringComparison.OrdinalIgnoreCase) || appCode.Contains("549981C3F5F10"))
                    return "Cortana (Voice Assistant)";
                if (name.Contains("OneDrive", StringComparison.OrdinalIgnoreCase))
                    return "OneDrive (Cloud Storage Sync)";
                if (name.Contains("DevHome", StringComparison.OrdinalIgnoreCase))
                    return "Dev Home (Developer Dashboard)";
                if (name.Contains("Family", StringComparison.OrdinalIgnoreCase))
                    return "Family Safety (Parental Controls)";
                if (name.Contains("ToDo", StringComparison.OrdinalIgnoreCase) || name.Contains("Todos", StringComparison.OrdinalIgnoreCase))
                    return "Microsoft To Do (Tasks List)";
                if (name.Contains("Whiteboard", StringComparison.OrdinalIgnoreCase))
                    return "Whiteboard (Digital Collaboration)";
                if (name.Contains("People", StringComparison.OrdinalIgnoreCase))
                    return "People (Contacts App)";
                if (name.Contains("Feedback", StringComparison.OrdinalIgnoreCase))
                    return "Feedback Hub (Send Feedback)";
                if (name.Contains("Snipping", StringComparison.OrdinalIgnoreCase) || name.Contains("ScreenSketch", StringComparison.OrdinalIgnoreCase))
                    return "Snipping Tool (Screenshot App)";
                if (name.Contains("Alarms", StringComparison.OrdinalIgnoreCase))
                    return "Alarms and Clock";
                if (name.Contains("SoundRecorder", StringComparison.OrdinalIgnoreCase))
                    return "Voice Recorder (Audio Recording)";
                if (name.Contains("ZuneVideo", StringComparison.OrdinalIgnoreCase))
                    return "Movies and TV (Video Player)";
                if (name.Contains("ZuneMusic", StringComparison.OrdinalIgnoreCase))
                    return "Groove Music (Music Player)";
                if (name.Contains("Skype", StringComparison.OrdinalIgnoreCase))
                    return "Skype (Video Calls)";
                if (name.Contains("YourPhone", StringComparison.OrdinalIgnoreCase))
                    return "Phone Link (Android/iPhone Sync)";
                if (name.Contains("Gaming", StringComparison.OrdinalIgnoreCase))
                    return "Gaming Services (Xbox Game Services)";
                if (name.Contains("PowerAutomate", StringComparison.OrdinalIgnoreCase))
                    return "Power Automate (Workflow Automation)";
                if (name.Contains("GetHelp", StringComparison.OrdinalIgnoreCase))
                    return "Get Help (Windows Support)";
                if (name.Contains("Getstarted", StringComparison.OrdinalIgnoreCase))
                    return "Tips (Windows Getting Started)";
                if (name.Contains("MixedReality", StringComparison.OrdinalIgnoreCase))
                    return "Mixed Reality Portal (VR Headset)";
                if (name.Contains("3DViewer", StringComparison.OrdinalIgnoreCase))
                    return "3D Model Viewer";
                if (name.Contains("HEIF", StringComparison.OrdinalIgnoreCase))
                    return "HEIF Image Extension (Photo Format)";
                if (name.Contains("HEVC", StringComparison.OrdinalIgnoreCase))
                    return "HEVC Video Extension (H.265 Video)";
                if (name.Contains("VP9", StringComparison.OrdinalIgnoreCase))
                    return "VP9 Video Extension (Web Video)";
                if (name.Contains("WebMedia", StringComparison.OrdinalIgnoreCase))
                    return "Web Media Extensions (Web Codecs)";
                if (name.Contains("AV1", StringComparison.OrdinalIgnoreCase))
                    return "AV1 Video Extension (Next-Gen Codec)";
                if (name.Contains("Mail", StringComparison.OrdinalIgnoreCase) || name.Contains("Communications", StringComparison.OrdinalIgnoreCase))
                    return "Mail and Calendar (Outlook Lite)";
                
                return string.IsNullOrWhiteSpace(name) ? appCode : name;
            }

            return appCode;
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in _appItems)
            {
                app.CheckBox.IsChecked = true;
            }
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in _appItems)
            {
                app.CheckBox.IsChecked = false;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            // If dialog is closed with X button, treat it as Cancel
            // Only OK button should set DialogResult to true
            if (!DialogResult.HasValue)
            {
                DialogResult = false;
            }
        }

        public List<string> GetSelectedApps()
        {
            return _appItems.Where(a => a.CheckBox.IsChecked == true).Select(a => a.Code).ToList();
        }

        private class AppItem
        {
            public string Code { get; set; } = string.Empty;
            public CheckBox CheckBox { get; set; } = new();
        }
    }
}
