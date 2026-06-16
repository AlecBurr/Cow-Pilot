using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CowPilot;

static class UpdateChecker
{
    private const string LatestManifestApiUrl = "https://api.github.com/repos/AlecBurr/Cow-Pilot/contents/release/latest.json?ref=main";

    public static async Task<UpdateCheckResult?> CheckLatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CowPilot", AppVersion.Version));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            string? token = Environment.GetEnvironmentVariable("COW_PILOT_GITHUB_TOKEN");
            if (string.IsNullOrWhiteSpace(token)) token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrWhiteSpace(token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.GetAsync(LatestManifestApiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!TryReadLatestManifestFromGitHubContent(json, out string latestVersion, out string? downloadUrl)) return null;
            return new UpdateCheckResult(IsNewerVersion(latestVersion, AppVersion.Version), latestVersion, downloadUrl);
        }
        catch
        {
            return null;
        }
    }

    public static bool TryReadLatestManifestFromGitHubContent(string json, out string latestVersion, out string? downloadUrl)
    {
        latestVersion = "";
        downloadUrl = null;
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("content", out JsonElement content)) return TryReadLatestManifest(json, out latestVersion, out downloadUrl);
        string encoded = (content.GetString() ?? "").Replace("\n", "").Replace("\r", "");
        if (encoded.Length == 0) return false;
        string manifestJson = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        return TryReadLatestManifest(manifestJson, out latestVersion, out downloadUrl);
    }

    public static bool TryReadLatestManifest(string json, out string latestVersion, out string? downloadUrl)
    {
        latestVersion = "";
        downloadUrl = null;
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("version", out JsonElement version)) return false;
        latestVersion = CleanVersion(version.GetString() ?? "");
        if (root.TryGetProperty("url", out JsonElement url)) downloadUrl = url.GetString();
        return latestVersion.Length > 0;
    }

    public static bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        return Version.TryParse(CleanVersion(latestVersion), out Version? latest)
            && Version.TryParse(CleanVersion(currentVersion), out Version? current)
            && latest > current;
    }

    private static string CleanVersion(string version)
    {
        version = version.Trim();
        if (version.StartsWith('v') || version.StartsWith('V')) version = version[1..];
        int suffix = version.IndexOfAny(['-', '+']);
        return suffix >= 0 ? version[..suffix] : version;
    }
}

sealed record UpdateCheckResult(bool IsNewer, string LatestVersion, string? DownloadUrl);
