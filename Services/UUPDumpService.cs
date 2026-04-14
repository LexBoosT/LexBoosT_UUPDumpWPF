using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Web;
using UUPDumpWPF.Models;

namespace UUPDumpWPF.Services
{
    public class UUPDumpService
    {
        private readonly HttpClient _httpClient;

        public UUPDumpService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
        }

        public async Task<List<Build>> GetBuildsAsync(string searchQuery)
        {
            try
            {
                var encodedQuery = HttpUtility.UrlEncode(searchQuery);
                var url = $"https://api.uupdump.net/listid.php?search={encodedQuery}";
                System.Diagnostics.Debug.WriteLine($"[UUPDumpService] Fetching: {url}");
                
                var response = await _httpClient.GetStringAsync(url);
                System.Diagnostics.Debug.WriteLine($"[UUPDumpService] Response length: {response.Length} chars");
                
                var json = JsonNode.Parse(response);
                if (json?["response"]?["builds"] == null)
                {
                    var error = json?["response"]?["error"]?.ToString() ?? "Unknown error";
                    System.Diagnostics.Debug.WriteLine($"[UUPDumpService] API Error: {error}");
                    throw new Exception($"API Error: {error}");
                }

                var buildsNode = json["response"]!["builds"]!;
                var buildList = new List<(string Id, string Title, string BuildNumber, bool IsRetail, string Architecture)>();
                
                System.Diagnostics.Debug.WriteLine($"[UUPDumpService] Parsing builds from API...");

                foreach (var property in buildsNode.AsObject())
                {
                    var buildData = property.Value;
                    if (buildData == null) continue;

                    var title = buildData["title"]?.ToString() ?? "";
                    var arch = buildData["arch"]?.ToString() ?? "unknown";
                    var buildNumber = buildData["build"]?.ToString() ?? "";

                    // Determine Retail/Preview from title keywords
                    var isRetail = DetermineRingFromTitle(title) == "Retail";
                    
                    System.Diagnostics.Debug.WriteLine($"[UUPDumpService] Build: {buildNumber}, Title: {title}, Arch: {arch}, Retail: {isRetail}");

                    buildList.Add((
                        Id: buildData["uuid"]?.ToString() ?? "",
                        Title: title,
                        BuildNumber: buildNumber,
                        IsRetail: isRetail,
                        Architecture: arch
                    ));
                }
                
                System.Diagnostics.Debug.WriteLine($"[UUPDumpService] Total builds parsed: {buildList.Count}");

                // Sort builds by:
                // 1. Build number (newest first)
                // 2. Priority: Standard Windows 11 > CPC OS > Enterprise > Other variants
                var sortedBuilds = buildList
                    .OrderByDescending(b =>
                    {
                        var parts = b.BuildNumber.Split('.');
                        if (parts.Length >= 2 && double.TryParse(parts[0], out var major) &&
                            double.TryParse(parts[1], out var minor))
                            return major + minor / 10000.0;
                        return 0;
                    })
                    .ThenBy(b => GetBuildSortPriority(b.Title))
                    .Take(200)
                    .ToList();

                // Remove duplicates based on BuildNumber, Architecture, Retail/Preview type, AND build variant
                // This is important because the same build number can have:
                // - Both amd64 and arm64 versions
                // - Both Retail and Preview versions for the same build/architecture
                // - Different build variants (e.g., "Windows 11" vs "Feature update for Windows CPC OS")
                var uniqueBuilds = sortedBuilds
                    .GroupBy(b => $"{b.BuildNumber}_{b.Architecture}_{(b.IsRetail ? "Retail" : "Preview")}_{GetBuildVariantKey(b.Title)}")
                    .Select(g => g.First())
                    .ToList();

                // Create Build objects
                var builds = uniqueBuilds.Select(b => new Build
                {
                    Id = b.Id,
                    Title = b.Title,
                    BuildNumber = b.BuildNumber,
                    IsRetail = b.IsRetail,
                    Architecture = b.Architecture
                }).ToList();

                return builds;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to fetch builds: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Determines if the build is Retail or Preview based on title keywords
        /// </summary>
        private string DetermineRingFromTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return "Retail";

            var titleLower = title.ToLowerInvariant();

            // ========== PREVIEW VERSIONS ==========
            // "Preview Update for Windows", "Cumulative Update Preview", "Insider Preview"
            if (titleLower.Contains("preview update for windows") ||
                titleLower.Contains("cumulative update preview") ||
                titleLower.Contains("insider preview") ||
                titleLower.Contains("release preview"))
                return "Preview";

            // ========== RETAIL VERSIONS ==========
            // "Security Update", "Cumulative Update" (without Preview), "Windows 11, version"
            // "Feature Update for Windows", "Feature Update for Windows CPC OS", etc.
            if (titleLower.Contains("security update") ||
                titleLower.Contains("cumulative update") ||
                titleLower.Contains("update for windows") ||
                titleLower.Contains("feature update") ||
                titleLower.Contains("windows 11, version") ||
                titleLower.Contains("windows 10, version") ||
                titleLower.Contains("enabled editions") ||
                titleLower.Contains("cpc os"))
                return "Retail";

            // Default to Retail
            return "Retail";
        }

        /// <summary>
        /// Extracts a variant key from the build title to differentiate between builds
        /// with the same build number and architecture but different purposes.
        /// Examples: "Windows 11", "Windows CPC OS", "Enterprise", etc.
        /// </summary>
        private string GetBuildVariantKey(string title)
        {
            if (string.IsNullOrEmpty(title))
                return "unknown";

            var titleLower = title.ToLowerInvariant();

            // CPC OS (Cloud PC)
            if (titleLower.Contains("cpc os"))
                return "cpc_os";

            // Enterprise editions
            if (titleLower.Contains("enterprise"))
                return "enterprise";

            // Education editions
            if (titleLower.Contains("education"))
                return "education";

            // IoT editions
            if (titleLower.Contains("iot"))
                return "iot";

            // Standard Windows 11/10
            if (titleLower.Contains("windows 11") || titleLower.Contains("windows 10"))
                return "standard_windows";

            // Feature updates
            if (titleLower.Contains("feature update"))
                return "feature_update";

            // Security/Cumulative updates
            if (titleLower.Contains("security update") || titleLower.Contains("cumulative update"))
                return "update";

            // Enabled editions
            if (titleLower.Contains("enabled editions"))
                return "enabled_editions";

            // Default: use first 50 chars of title
            return titleLower.Length > 50 ? titleLower.Substring(0, 50).Replace(" ", "_") : titleLower.Replace(" ", "_");
        }

        /// <summary>
        /// Returns sort priority for builds. Lower number = higher priority.
        /// Standard Windows 11 should appear first, then CPC OS, then other variants.
        /// </summary>
        private int GetBuildSortPriority(string title)
        {
            if (string.IsNullOrEmpty(title))
                return 99;

            var titleLower = title.ToLowerInvariant();

            // Highest priority: Standard Windows 11/10
            if (titleLower.Contains("windows 11, version") || titleLower.Contains("windows 10, version"))
                return 1;

            // Second priority: CPC OS (Cloud PC)
            if (titleLower.Contains("cpc os"))
                return 2;

            // Third priority: Enterprise editions
            if (titleLower.Contains("enterprise"))
                return 3;

            // Fourth priority: Education editions
            if (titleLower.Contains("education"))
                return 4;

            // Fifth priority: IoT editions
            if (titleLower.Contains("iot"))
                return 5;

            // Sixth priority: Feature updates
            if (titleLower.Contains("feature update"))
                return 6;

            // Seventh priority: Security/Cumulative updates
            if (titleLower.Contains("security update") || titleLower.Contains("cumulative update"))
                return 7;

            // Eighth priority: Enabled editions
            if (titleLower.Contains("enabled editions"))
                return 8;

            // Default: lowest priority
            return 99;
        }

        public async Task<List<Language>> GetLanguagesAsync(string buildId)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(
                    $"https://api.uupdump.net/listlangs.php?id={buildId}");

                var json = JsonNode.Parse(response);
                if (json?["response"]?["langFancyNames"] == null)
                    return new List<Language>();

                var languages = new List<Language>();
                var langNames = json["response"]!["langFancyNames"]!.AsObject();

                foreach (var property in langNames)
                {
                    languages.Add(new Language
                    {
                        Code = property.Key,
                        Name = property.Value?.ToString() ?? ""
                    });
                }

                return languages;
            }
            catch
            {
                return new List<Language>();
            }
        }

        public async Task<List<Edition>> GetEditionsAsync(string buildId, string language)
        {
            try
            {
                var encodedLang = HttpUtility.UrlEncode(language);
                var response = await _httpClient.GetStringAsync(
                    $"https://api.uupdump.net/listeditions.php?id={buildId}&lang={encodedLang}");

                var json = JsonNode.Parse(response);
                if (json?["response"]?["editionFancyNames"] == null)
                    return new List<Edition>();

                var editions = new List<Edition>();
                var editionNames = json["response"]!["editionFancyNames"]!.AsObject();

                foreach (var property in editionNames)
                {
                    editions.Add(new Edition
                    {
                        Code = property.Key,
                        Name = property.Value?.ToString() ?? ""
                    });
                }

                // Add virtual editions based on the base edition
                // These are additional editions that can be created from the base edition
                // Note: Virtual editions are NOT added to the list anymore - they will be shown in a separate section
                var hasProEdition = editions.Any(e => e.Code.Equals("professional", StringComparison.OrdinalIgnoreCase));
                var hasHomeEdition = editions.Any(e => e.Code.Equals("core", StringComparison.OrdinalIgnoreCase));
                var hasProNEdition = editions.Any(e => e.Code.Equals("professionaln", StringComparison.OrdinalIgnoreCase));

                // Store virtual edition info for UI (not added to list)
                if (hasProEdition)
                {
                    editions.Add(new Edition { 
                        Code = "_VIRTUAL_PRO", 
                        Name = ">>> Windows Pro - Additional Editions Available <<<", 
                        IsVirtual = false,
                        BaseEditionCode = "professional"
                    });
                }

                return editions;
            }
            catch
            {
                return new List<Edition>();
            }
        }
    }
}
