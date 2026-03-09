using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Web;
using UUPDumpWPF.Models;

namespace UUPDumpWPF.Services
{
    public class UUPDumpService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ConcurrentDictionary<string, string> _archCache;

        public UUPDumpService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            _archCache = new ConcurrentDictionary<string, string>();
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
                var buildList = new List<(string Id, string Title, string BuildNumber, bool IsRetail, string Ring)>();

                foreach (var property in buildsNode.AsObject())
                {
                    var buildData = property.Value;
                    if (buildData == null) continue;

                    var title = buildData["title"]?.ToString() ?? "";
                    var isRetail = !title.Contains("Preview", StringComparison.OrdinalIgnoreCase);
                    var ring = buildData["ring"]?.ToString() ?? "UNKNOWN";

                    buildList.Add((
                        Id: buildData["uuid"]?.ToString() ?? "",
                        Title: title,
                        BuildNumber: buildData["build"]?.ToString() ?? "",
                        IsRetail: isRetail,
                        Ring: ring
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

                // Fetch architectures in parallel
                var architectureTasks = sortedBuilds.Select(async b =>
                {
                    if (_archCache.TryGetValue(b.Id, out var cachedArch))
                        return (b, Architecture: cachedArch);

                    var arch = await GetArchitectureAsync(b.Id);
                    _archCache.TryAdd(b.Id, arch);
                    return (b, Architecture: arch);
                });

                var results = await Task.WhenAll(architectureTasks);

                var builds = results.Select(r => new Build
                {
                    Id = r.b.Id,
                    Title = r.b.Title,
                    BuildNumber = r.b.BuildNumber,
                    IsRetail = r.b.IsRetail,
                    Ring = r.b.Ring,
                    Architecture = r.Architecture
                }).ToList();

                return builds;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to fetch builds: {ex.Message}", ex);
            }
        }

        private async Task<string> GetArchitectureAsync(string buildId)
        {
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        var response = await _httpClient.GetStringAsync(
                            $"https://api.uupdump.net/listlangs.php?id={buildId}");

                        var json = JsonNode.Parse(response);
                        if (json?["response"]?["updateInfo"]?["arch"] != null)
                            return json["response"]!["updateInfo"]!["arch"]!.ToString();

                        if (i < 2)
                        {
                            await Task.Delay(500);
                            continue;
                        }
                    }
                    catch
                    {
                        if (i < 2)
                        {
                            await Task.Delay(500);
                            continue;
                        }
                        throw;
                    }
                }
            }
            catch { }

            return "amd64"; // Default fallback
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

                return editions;
            }
            catch
            {
                return new List<Edition>();
            }
        }
    }
}
