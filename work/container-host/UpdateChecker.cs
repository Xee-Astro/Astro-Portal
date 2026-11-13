using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AsiairContainer;

public sealed record OfficialRelease(string Version, string SourceUrl);
public sealed record UpdateSnapshot(DateTimeOffset LastAttempt, DateTimeOffset? LastSuccess, OfficialRelease? Release, string? Error);
public sealed class UpdateChecker : IDisposable
{
    public const string OfficialUrl = "https://www.zwoastro.com/software/asiair/";
    public const string StellavitaUrl = "https://www.touptek-astro.com/api/StellaVita/readfile_StellaVita_Changelog.php";
    private readonly bool stellavita;
    private readonly string cachePath;
    private readonly HttpClient http;
    private readonly SemaphoreSlim checkLock = new(1, 1);
    public UpdateSnapshot? Snapshot { get; private set; }
    public UpdateChecker(string root, HttpMessageHandler? handler = null, bool stellavita = false)
    {
        this.stellavita = stellavita;
        cachePath = Path.Combine(root, "private-data", stellavita ? "stellavita-update-check.json" : "update-check.json");
        http = new HttpClient(handler ?? new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(25) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Astro-Portal/0.5.0 (official app release checker)");
        try
        {
            if (File.Exists(cachePath)) Snapshot = JsonSerializer.Deserialize<UpdateSnapshot>(File.ReadAllText(cachePath));
            if (Snapshot is { Error: null, Release: null } || Snapshot?.LastAttempt > DateTimeOffset.UtcNow.AddDays(1)) Snapshot = null;
            if (Snapshot?.Release is not null) _ = ParseVersion(Snapshot.Release.Version);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or FormatException or NullReferenceException) { Snapshot = null; }
    }
    public async Task<UpdateSnapshot> CheckAsync(bool force, CancellationToken token)
    {
        await checkLock.WaitAsync(token);
        try
        {
            if (!force && Snapshot is not null && DateTimeOffset.UtcNow - Snapshot.LastAttempt < (Snapshot.Error is null ? TimeSpan.FromHours(6) : TimeSpan.FromMinutes(30))) return Snapshot;
            var now = DateTimeOffset.UtcNow;
            try
            {
                using var response = await http.GetAsync(stellavita ? StellavitaUrl : OfficialUrl, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();
                if (response.RequestMessage?.RequestUri?.Scheme != "https" || response.RequestMessage.RequestUri.Host != (stellavita ? "www.touptek-astro.com" : "www.zwoastro.com")) throw new IOException("The release page redirected away from the official vendor site.");
                await using var stream = await response.Content.ReadAsStreamAsync(token);
                using var buffer = new MemoryStream(); byte[] chunk = new byte[16384]; int count;
                while ((count = await stream.ReadAsync(chunk, token)) > 0)
                {
                    if (buffer.Length + count > 2 * 1024 * 1024) throw new IOException("The official release page was unexpectedly large.");
                    buffer.Write(chunk, 0, count);
                }
                string body = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
                var release = stellavita ? ParseStellavitaFeed(body) : ParseOfficialPage(body);
                Snapshot = new(now, now, release, null);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException or FormatException or RegexMatchTimeoutException or JsonException)
            {
                Snapshot = new(now, Snapshot?.LastSuccess, Snapshot?.Release, "Update check unavailable. Check your internet connection or try again later.");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            string temporary = cachePath + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(Snapshot), token);
            File.Move(temporary, cachePath, true);
            return Snapshot;
        }
        finally { checkLock.Release(); }
    }
    public static bool IsUpdateAvailable(string? installed, UpdateSnapshot? snapshot)
    {
        try { return installed is not null && snapshot is { Error: null, Release: not null } && ParseVersion(snapshot.Release.Version) > ParseVersion(installed); }
        catch (FormatException) { return false; }
    }
    public static Version ParseVersion(string value)
    {
        value = value.Trim().TrimStart('v', 'V');
        if (!Regex.IsMatch(value, @"\A\d{1,6}(?:\.\d{1,6}){1,3}\z")) throw new FormatException("Unsupported release version.");
        int[] parts = value.Split('.').Select(int.Parse).ToArray();
        return new(parts[0], parts[1], parts.Length > 2 ? parts[2] : 0, parts.Length > 3 ? parts[3] : 0);
    }
    public static OfficialRelease ParseOfficialPage(string html)
    {
        if (html.Length > 2 * 1024 * 1024) throw new FormatException("Release page too large.");
        var regexOptions = RegexOptions.IgnoreCase | RegexOptions.Singleline;
        string[] cards = Regex.Split(html, "<div\\b[^>]*class\\s*=\\s*[\"'][^\"']*\\bzwo-download-card\\b[^\"']*[\"'][^>]*>", regexOptions, TimeSpan.FromSeconds(2));
        var releases = new List<(Version Parsed, string Text)>();
        foreach (string card in cards.Skip(1))
        {
            var platform = Regex.Match(card, "class=[\"'][^\"']*\\bzwo-platform\\b[^\"']*[\"'][^>]*>(.*?)</div>", regexOptions, TimeSpan.FromSeconds(2));
            string plain = WebUtility.HtmlDecode(Regex.Replace(card, "<[^>]*>", " ", regexOptions, TimeSpan.FromSeconds(2)));
            if (!platform.Success || Regex.Replace(platform.Groups[1].Value, "\\s+", "").Trim() != "Android" || Regex.IsMatch(plain, @"Edition\s*1|\bbeta\b|\balpha\b", RegexOptions.IgnoreCase)) continue;
            var version = Regex.Match(plain, @"\bVersion:\s*[vV]?(\d{1,6}(?:\.\d{1,6}){1,3})\b", regexOptions, TimeSpan.FromSeconds(2));
            if (version.Success) releases.Add((ParseVersion(version.Groups[1].Value), version.Groups[1].Value));
        }
        if (releases.Count == 0) throw new FormatException("ZWO's Android release information was not recognized.");
        return new(releases.MaxBy(x => x.Parsed).Text, OfficialUrl);
    }
    public static OfficialRelease ParseStellavitaFeed(string json)
    {
        if (json.Length > 2 * 1024 * 1024) throw new FormatException("Release data too large.");
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new FormatException("Unrecognized Stellavita release data.");
        var releases = new List<(Version Parsed, string Text)>();
        foreach (var entry in document.RootElement.EnumerateObject())
        {
            // The official feed mixes app APKs and controller firmware. Accept stable APK filenames only.
            var match = Regex.Match(entry.Name, @"\AStellaVita_(\d{1,6}(?:\.\d{1,6}){1,3})[_.]\d{8,9}\.apk\z", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
            if (match.Success) releases.Add((ParseVersion(match.Groups[1].Value), match.Groups[1].Value));
        }
        if (releases.Count == 0) throw new FormatException("Stellavita's stable Android release information was not recognized.");
        return new(releases.MaxBy(x => x.Parsed).Text, "https://www.touptek-astro.com/StellaVita/Changelog/");
    }
    public static string? InstalledVersion(string root, bool stellavita = false)
    {
        try { var installed = JsonSerializer.Deserialize<InstalledAppInfo>(File.ReadAllText(Path.Combine(root, "private-data", stellavita ? "installed-stellavita-info.json" : "installed-app-info.json"))); return installed?.Profile == ContainerOptions.Load(root).ProfileName && installed.Package == (stellavita ? "com.touptek.astrostation" : "com.zwoasi.asiair") ? installed.Version : null; }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }
    public void Dispose() { http.Dispose(); }
}
