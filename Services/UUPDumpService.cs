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
                var response = await _httpClient.GetStringAsync(
                    $"https://api.uupdump.net/listid.php?search={encodedQuery}");

                var json = JsonNode.Parse(response);
                if (json?["response"]?["builds"] == null)
                {
                    var error = json?["response"]?["error"]?.ToString() ?? "Unknown error";
                    throw new Exception($"API Error: {error}");
                }

                var buildsNode = json["response"]!["builds"]!;
                var buildList = new List<(string Id, string Title, string BuildNumber, bool IsRetail, string Architecture)>();

                foreach (var property in buildsNode.AsObject())
                {
                    var buildData = property.Value;
                    if (buildData == null) continue;

                    var title = buildData["title"]?.ToString() ?? "";
                    var isRetail = !title.Contains("Preview", StringComparison.OrdinalIgnoreCase);
                    var arch = buildData["arch"]?.ToString() ?? "unknown";

                    buildList.Add((
                        Id: buildData["uuid"]?.ToString() ?? "",
                        Title: title,
                        BuildNumber: buildData["build"]?.ToString() ?? "",
                        IsRetail: isRetail,
                        Architecture: arch
                    ));
                }

                // Sort and limit to top 50 builds for faster loading
                var sortedBuilds = buildList.OrderByDescending(b =>
                {
                    var parts = b.BuildNumber.Split('.');
                    if (parts.Length >= 2 && double.TryParse(parts[0], out var major) &&
                        double.TryParse(parts[1], out var minor))
                        return major + minor / 10000.0;
                    return 0;
                }).Take(50).ToList();

                // Remove duplicates based on BuildNumber AND Architecture (keep first occurrence of each build/arch combination)
                // This is important because the same build number can have both amd64 and arm64 versions
                var uniqueBuilds = sortedBuilds
                    .GroupBy(b => $"{b.BuildNumber}_{b.Architecture}")
                    .Select(g => g.First())
                    .ToList();

                // Architecture is already included in the API response, no need for additional calls
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
