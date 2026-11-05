using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace AsiairContainer;

public sealed class OfficialApkInstaller : IDisposable
{
    public const string StellavitaApkUrl = "https://www.touptek-astro.com/StellaVita.php";
    private readonly string root;
    private readonly HttpClient http;
    public OfficialApkInstaller(string root)
    {
        this.root = root;
        http = new(new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromMinutes(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Astro-Portal/0.5");
    }
    public static Uri ResolveAsiairDownload(string html)
    {
        // Select the same latest stable Android card as the release checker.
        var release = UpdateChecker.ParseOfficialPage(html);
        var cards = Regex.Split(html, "<div\\b[^>]*class\\s*=\\s*[\"'][^\"']*\\bzwo-download-card\\b[^\"']*[\"'][^>]*>", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
        foreach (var card in cards.Skip(1))
        {
            string plain = WebUtility.HtmlDecode(Regex.Replace(card, "<[^>]*>", " ", RegexOptions.Singleline, TimeSpan.FromSeconds(2)));
            var platform = Regex.Match(card, "class=[\"'][^\"']*\\bzwo-platform\\b[^\"']*[\"'][^>]*>(.*?)</div>", RegexOptions.Singleline, TimeSpan.FromSeconds(2));
            var version = Regex.Match(plain, @"\bVersion:\s*[vV]?(\d+(?:\.\d+){1,3})\b");
            if (!platform.Success || platform.Groups[1].Value.Trim() != "Android" || !version.Success || UpdateChecker.ParseVersion(version.Groups[1].Value) != UpdateChecker.ParseVersion(release.Version) || Regex.IsMatch(plain, @"Edition\s*1|\bbeta\b|\balpha\b", RegexOptions.IgnoreCase)) continue;
            foreach (Match link in Regex.Matches(card, "href=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2)))
            {
                if (!Uri.TryCreate(WebUtility.HtmlDecode(link.Groups[1].Value), UriKind.Absolute, out var uri) || uri.Scheme != "https") continue;
                if (uri.Host == "drive.google.com")
                {
                    var id = Regex.Match(uri.AbsolutePath, @"/file/d/([A-Za-z0-9_-]+)/");
                    if (id.Success) return new("https://drive.usercontent.google.com/download?id=" + id.Groups[1].Value + "&export=download&confirm=t");
                }
                if (uri.Host is "www.zwoastro.com" or "i.zwoastro.com" or "dl.zwoastro.com" && uri.AbsolutePath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) return uri;
            }
        }
        throw new IOException("The official ASIAIR APK link could not be identified. Try again later or use Advanced → Google Play.");
    }
    public static bool AllowedDownload(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 && (uri.Host is "www.touptek-astro.com" or "touptek-astro.com" or "www.zwoastro.com" or "i.zwoastro.com" or "dl.zwoastro.com" or "drive.google.com" or "drive.usercontent.google.com" || uri.Host.EndsWith(".googleusercontent.com", StringComparison.Ordinal));
    private async Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken token)
    {
        for (int i = 0; i < 8; i++)
        {
            if (!AllowedDownload(uri)) throw new IOException("The vendor download redirected to an unsupported source.");
            var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is Uri location) { var next = new Uri(uri, location); response.Dispose(); uri = next; continue; }
            response.EnsureSuccessStatusCode(); return response;
        }
        throw new IOException("Too many APK download redirects.");
    }
    public async Task<string> DownloadAsync(bool stella, Action<string> progress, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromMinutes(20)); token = deadline.Token;
        Uri uri = new(StellavitaApkUrl); string name = stella ? "Stellavita" : "ASIAIR";
        progress("Finding the official " + name + " download…");
        if (!stella)
        {
            using var page = await GetAsync(new(UpdateChecker.OfficialUrl), token);
            await using var stream = await page.Content.ReadAsStreamAsync(token); using var content = new MemoryStream();
            byte[] block = new byte[16384]; int n;
            while ((n = await stream.ReadAsync(block, token)) > 0) { if (content.Length + n > 2 * 1024 * 1024) throw new IOException("Official release page is too large."); content.Write(block, 0, n); }
            uri = ResolveAsiairDownload(System.Text.Encoding.UTF8.GetString(content.ToArray()));
        }
        string folder = Path.Combine(root, "private-data", "downloads"); Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, name + ".apk"), temporary = path + ".part";
        try
        {
            using var response = await GetAsync(uri, token);
            long? expected = response.Content.Headers.ContentLength;
            if (expected is > 2L * 1024 * 1024 * 1024) throw new IOException("The APK exceeds the download size limit.");
            await using (var input = await response.Content.ReadAsStreamAsync(token))
            await using (var output = File.Create(temporary))
            {
                byte[] buffer = new byte[1024 * 1024]; long total = 0; int count; var last = DateTimeOffset.MinValue;
                while ((count = await input.ReadAsync(buffer, token)) > 0)
                {
                    total += count; if (total > 2L * 1024 * 1024 * 1024) throw new IOException("The APK exceeds the download size limit.");
                    await output.WriteAsync(buffer.AsMemory(0, count), token);
                    if (DateTimeOffset.UtcNow - last > TimeSpan.FromMilliseconds(250)) { progress(expected > 0 ? $"Downloading {name}… {100 * total / expected}%" : $"Downloading {name}… {total / 1048576} MB"); last = DateTimeOffset.UtcNow; }
                }
                if (total < 1024 || expected.HasValue && total != expected) throw new IOException("The APK download is incomplete.");
            }
            VerifyApk(temporary, stella); File.Move(temporary, path, true); return path;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static void VerifyApk(string path, bool stella)
    {
        // Android verifies the complete APK signature during installation. Pin the vendor
        // signer certificate here too, before allowing any fresh installation.
        string expected = stella ? "4B0D54CAF18140B1FFE67A1C591DCE5B2A251F0EB8EF1C6055CEC414849B4A09" : "EF35A76DF8D815E0B0C15070966D5F29F2F6E1055CCEC26BE41CC77004EC9356";
        if (!string.Equals(ReadSignerHash(path), expected, StringComparison.Ordinal)) throw new IOException("The APK vendor certificate does not match. Installation was blocked.");
        if (ApkIdentity.Read(path).Package != (stella ? RuntimeController.StellavitaPackage : RuntimeController.AsiairPackage)) throw new IOException("The APK contains a different app than requested.");
        using var zip = ZipFile.OpenRead(path);
        if (zip.GetEntry("AndroidManifest.xml") is null || zip.GetEntry("classes.dex") is null || !zip.Entries.Any(e => e.FullName.StartsWith("lib/x86_64/", StringComparison.Ordinal))) throw new IOException("The APK does not contain the required Android/x86-64 components.");
    }
    public static string ReadSignerHash(string path)
    {
        using var stream = File.OpenRead(path); if (stream.Length < 32) throw new IOException("Invalid APK.");
        byte[] tail = new byte[(int)Math.Min(stream.Length, 65557)]; stream.Position = stream.Length - tail.Length; stream.ReadExactly(tail);
        int eocd = -1;
        for (int i = tail.Length - 22; i >= 0; i--) if (BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i)) == 0x06054b50 && i + 22 + BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(i + 20)) == tail.Length) { eocd = i; break; }
        if (eocd < 0) throw new IOException("Invalid APK directory.");
        long central = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(eocd + 16)); if (central < 24 || central > stream.Length) throw new IOException("Invalid APK signing block.");
        stream.Position = central - 24; byte[] footer = new byte[24]; stream.ReadExactly(footer);
        long size = BinaryPrimitives.ReadInt64LittleEndian(footer);
        if (!footer.AsSpan(8).SequenceEqual("APK Sig Block 42"u8) || size < 24 || size > 16 * 1024 * 1024 || size + 8 > central) throw new IOException("The APK has no supported signing block.");
        stream.Position = central - size - 8; byte[] block = new byte[(int)size + 8]; stream.ReadExactly(block);
        if (BinaryPrimitives.ReadInt64LittleEndian(block) != size) throw new IOException("Invalid signing block length.");
        int position = 8;
        while (position < block.Length - 24)
        {
            long length = BinaryPrimitives.ReadInt64LittleEndian(block.AsSpan(position));
            if (length < 4 || length > block.Length - 24 - position - 8) throw new IOException("Invalid APK signer entry.");
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(position + 8));
            if (id == 0x7109871a)
            {
                ReadOnlySpan<byte> value = block.AsSpan(position + 12, (int)length - 4);
                var signers = Field(ref value); var signer = Field(ref signers); var signed = Field(ref signer);
                _ = Field(ref signed); var certificates = Field(ref signed); var certificate = Field(ref certificates);
                return Convert.ToHexString(SHA256.HashData(certificate));
            }
            position += (int)length + 8;
        }
        throw new IOException("The APK has no supported vendor signature.");
    }
    private static ReadOnlySpan<byte> Field(ref ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4) throw new IOException("Invalid signer data.");
        uint n = BinaryPrimitives.ReadUInt32LittleEndian(bytes); if (n > bytes.Length - 4) throw new IOException("Invalid signer length.");
        var result = bytes.Slice(4, (int)n); bytes = bytes[(4 + (int)n)..]; return result;
    }
    public void Dispose() => http.Dispose();
}
