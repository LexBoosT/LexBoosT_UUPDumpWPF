using Microsoft.Win32;
using System.Text.RegularExpressions;
using UUPDumpWPF.Models;

namespace UUPDumpWPF.Services
{
    public class WindowsVersionService
    {
        public WindowsVersion GetCurrentVersion()
        {
            const string registryPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            
            using var key = Registry.LocalMachine.OpenSubKey(registryPath);
            if (key == null)
                return new WindowsVersion { FullVersion = "Unknown" };

            var baseBuild = key.GetValue("CurrentBuild")?.ToString() ?? "0";
            var ubr = key.GetValue("UBR")?.ToString();
            var displayVersion = key.GetValue("DisplayVersion")?.ToString() ?? "";
            var releaseId = key.GetValue("ReleaseId")?.ToString() ?? "";
            var productName = key.GetValue("ProductName")?.ToString() ?? "";
            var editionId = key.GetValue("EditionId")?.ToString() ?? "";

            var fullBuild = !string.IsNullOrEmpty(ubr) ? $"{baseBuild}.{ubr}" : baseBuild;

            var osName = int.Parse(baseBuild) >= 22000 ? "Windows 11" : "Windows 10";

            // Prioritize DisplayVersion if it contains a valid version format (e.g., "25H2", "24H2", "23H2")
            string versionName;
            if (!string.IsNullOrEmpty(displayVersion) && 
                System.Text.RegularExpressions.Regex.IsMatch(displayVersion, @"^\d{2}H[12]$"))
            {
                versionName = displayVersion;
            }
            else if (!string.IsNullOrEmpty(displayVersion) && 
                     System.Text.RegularExpressions.Regex.IsMatch(displayVersion, @"^\d{2}H\d$"))
            {
                versionName = displayVersion;
            }
            else
            {
                // Fallback to build number detection
                versionName = int.Parse(baseBuild) switch
                {
                    >= 26200 => "28H2",
                    >= 26100 => "24H2",
                    >= 22631 => "23H2",
                    >= 22621 => "22H2",
                    >= 22000 => "21H2",
                    >= 19045 => "22H2 (Win10)",
                    >= 19044 => "21H2 (Win10)",
                    _ => !string.IsNullOrEmpty(displayVersion) ? displayVersion :
                         !string.IsNullOrEmpty(releaseId) ? releaseId : "Unknown"
                };
            }

            return new WindowsVersion
            {
                Build = fullBuild,
                Version = $"{osName} {versionName}",
                Edition = editionId,
                Product = productName,
                FullVersion = $"{osName} {versionName} Build {fullBuild}"
            };
        }
    }
}
