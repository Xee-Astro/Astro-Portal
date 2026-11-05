using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace AsiairContainer;

public sealed record ContainerOptions
{
    public string ApkFolder { get; init; } = "";
    public string DeviceIp { get; init; } = "192.168.4.1";
    public int MemoryMb { get; init; } = 4096;
    public int CpuCores { get; init; } = 4;
    public int ConsolePort { get; init; } = 5560;
    public int AdbPort { get; init; } = 5039;
    public bool EnablePlayStore { get; init; } = true;
    public int AndroidApi { get; init; } = 30;
    public bool AutoStartAndroid { get; init; }
    public string PhoneGpsIp { get; init; } = "";
    public int PhoneGpsPort { get; init; } = 11123;
    public bool UsePhoneLocation { get; init; }
    public bool AutomaticUpdateChecks { get; init; } = true;
    public bool UseWindowsLocation { get; init; } = true;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string LocationSource { get; init; } = "Not set";
    [System.Text.Json.Serialization.JsonIgnore]
    public string ProfileName => EnablePlayStore ? (AndroidApi == 30 ? "Astro_Play" : "Astro_Play_" + AndroidApi) : "ASIAIR_Only";
    public void Validate()
    {
        if (!IPAddress.TryParse(DeviceIp, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork) throw new ArgumentException("Enter a valid IPv4 device address.");
        if (PhoneGpsPort is < 1 or > 65535 || PhoneGpsIp.Length > 0 && !IPAddress.TryParse(PhoneGpsIp, out _)) throw new ArgumentException("Enter a valid phone IP address and port (1–65535).");
        if (UsePhoneLocation && (PhoneGpsIp.Length == 0 || UseWindowsLocation)) throw new ArgumentException("Choose one location source and supply the phone IP address.");
        if (MemoryMb is < 1536 or > 8192 || CpuCores is < 1 or > 8) throw new ArgumentException("Unsupported runtime memory or CPU setting.");
        if (AndroidApi is not (30 or 33)) throw new ArgumentException("Unsupported Android profile version.");
        if (Latitude.HasValue != Longitude.HasValue || Latitude is double lat && (!double.IsFinite(lat) || lat is < -90 or > 90) || Longitude is double lon && (!double.IsFinite(lon) || lon is < -180 or > 180)) throw new ArgumentException("Enter latitude from -90 to 90 and longitude from -180 to 180.");
        if (ConsolePort is < 5554 or > 5680 || ConsolePort % 2 != 0 || AdbPort is < 1024 or > 65535 || AdbPort == ConsolePort || AdbPort == ConsolePort + 1 || AdbPort == 5037)
            throw new ArgumentException("The private runtime ports are invalid.");
    }
    public static ContainerOptions Load(string root)
    {
        string path = Path.Combine(root, "container.json");
        var result = File.Exists(path) ? JsonSerializer.Deserialize<ContainerOptions>(File.ReadAllText(path)) ?? new() : new();
        result.Validate(); return result;
    }
    public void Save(string root) { Validate(); File.WriteAllText(Path.Combine(root, "container.json"), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
}
