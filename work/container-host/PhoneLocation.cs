using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AsiairContainer;

public static class PhoneLocation
{
    public static async Task<LocationFix> GetAsync(string ip, int port, CancellationToken token)
    {
        if (!IPAddress.TryParse(ip, out var address) || port is < 1 or > 65535) throw new ArgumentException("Enter the phone's IP address and GPS2IP port.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            using var client = new TcpClient(address.AddressFamily);
            await client.ConnectAsync(address, port, timeout.Token);
            await using var stream = client.GetStream();
            byte[] buffer = new byte[2048]; var line = new StringBuilder();
            while (true)
            {
                int count = await stream.ReadAsync(buffer, timeout.Token);
                if (count == 0) throw new InvalidOperationException("The phone disconnected before sending a current GPS fix.");
                for (int i = 0; i < count; i++)
                {
                    char c = (char)buffer[i];
                    if (c == '$') line.Clear();
                    if (c is '\r' or '\n')
                    {
                        if (TryParse(line.ToString(), DateTimeOffset.UtcNow, out var fix)) return fix!;
                        line.Clear();
                    }
                    else if (line.Length < 1024) line.Append(c);
                    else line.Clear();
                }
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { throw new TimeoutException("No current GPS fix received. In GPS2IP select NMEA and Socket/TCP server, enable GGA or RMC, and check the phone IP and port."); }
    }
    public static bool TryParse(string sentence, DateTimeOffset now, out LocationFix? fix)
    {
        fix = null;
        int star = sentence.IndexOf('*');
        if (sentence.Length > 1024 || !sentence.StartsWith('$') || star < 1 || star + 3 != sentence.Length || !byte.TryParse(sentence.AsSpan(star + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte checksum)) return false;
        int actual = 0; for (int i = 1; i < star; i++) actual ^= sentence[i];
        if (actual != checksum) return false;
        string[] fields = sentence[1..star].Split(',');
        bool rmc = fields[0].Length == 5 && fields[0].EndsWith("RMC", StringComparison.Ordinal);
        bool gga = fields[0].Length == 5 && fields[0].EndsWith("GGA", StringComparison.Ordinal);
        if ((!rmc && !gga) || fields.Length < (rmc ? 10 : 7)) return false;
        if (rmc ? fields[2] != "A" : !int.TryParse(fields[6], out int quality) || quality < 1 || quality > 8) return false;
        int offset = rmc ? 3 : 2;
        if (!Coordinate(fields[offset], fields[offset + 1], true, out double lat) || !Coordinate(fields[offset + 2], fields[offset + 3], false, out double lon)) return false;
        string time = fields[1];
        if (time.Length < 6 || !int.TryParse(time[..2], out int hours) || !int.TryParse(time.Substring(2, 2), out int minutes) || !double.TryParse(time[4..], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double seconds) || hours > 23 || minutes > 59 || seconds is < 0 or >= 60) return false;
        DateTimeOffset timestamp;
        if (rmc)
        {
            if (!DateTime.TryParseExact(fields[9], "ddMMyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return false;
            timestamp = new DateTimeOffset(date, TimeSpan.Zero).AddHours(hours).AddMinutes(minutes).AddSeconds(seconds);
        }
        else
        {
            timestamp = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddHours(hours).AddMinutes(minutes).AddSeconds(seconds);
            if (timestamp - now > TimeSpan.FromHours(12)) timestamp = timestamp.AddDays(-1);
            if (now - timestamp > TimeSpan.FromHours(12)) timestamp = timestamp.AddDays(1);
        }
        if ((timestamp - now).Duration() > TimeSpan.FromMinutes(2)) return false;
        fix = new(lat, lon, double.NaN, timestamp); return true;
    }
    private static bool Coordinate(string text, string direction, bool latitude, out double result)
    {
        result = 0;
        if (direction != (latitude ? "N" : "E") && direction != (latitude ? "S" : "W")) return false;
        if (!double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double raw) || !double.IsFinite(raw) || raw < 0) return false;
        double degrees = Math.Floor(raw / 100), minutes = raw - degrees * 100;
        if (minutes >= 60) return false;
        result = degrees + minutes / 60;
        if (result > (latitude ? 90 : 180)) return false;
        if (direction is "S" or "W") result = -result;
        return true;
    }
}
