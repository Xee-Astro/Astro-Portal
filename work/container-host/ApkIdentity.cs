using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace AsiairContainer;
public sealed record ApkIdentity(string Package, long VersionCode)
{
    public static ApkIdentity Read(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var entry = zip.GetEntry("AndroidManifest.xml") ?? throw new IOException("The APK manifest is missing.");
        if (entry.Length > 4 * 1024 * 1024) throw new IOException("The APK manifest is too large.");
        using var input = entry.Open(); using var output = new MemoryStream(); input.CopyTo(output); byte[] data = output.ToArray();
        ushort U16(int p) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(p, 2));
        uint U32(int p) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(p, 4));
        if (data.Length < 8 || U16(0) != 3 || U32(4) != data.Length) throw new IOException("Invalid Android binary manifest.");
        string[] strings = [];
        string String(uint index) => index < strings.Length ? strings[index] : "";
        int Length8(ref int p) { int n = data[p++]; return (n & 128) == 0 ? n : ((n & 127) << 8) | data[p++]; }
        int Length16(ref int p) { int n = U16(p); p += 2; if ((n & 32768) == 0) return n; int low = U16(p); p += 2; return ((n & 32767) << 16) | low; }
        for (int position = 8; position + 8 <= data.Length;)
        {
            int header = U16(position + 2); uint rawSize = U32(position + 4);
            if (rawSize < header || header < 8 || rawSize > data.Length - position) throw new IOException("Invalid Android manifest chunk.");
            int size = (int)rawSize;
            if (U16(position) == 1)
            {
                if (header < 28) throw new IOException("Invalid Android string pool.");
                uint count = U32(position + 8); if (count > 100000 || header + (long)count * 4 > size) throw new IOException("Invalid Android string count.");
                bool utf8 = (U32(position + 16) & 256) != 0; uint start = U32(position + 20); strings = new string[count];
                for (int i = 0; i < strings.Length; i++)
                {
                    long offset = position + (long)start + U32(position + header + i * 4);
                    if (offset < position || offset >= position + size) throw new IOException("Invalid Android string offset.");
                    int p = (int)offset, length;
                    if (utf8) { _ = Length8(ref p); length = Length8(ref p); }
                    else length = checked(Length16(ref p) * 2);
                    if (length < 0 || p + length > position + size) throw new IOException("Invalid Android string length.");
                    strings[i] = (utf8 ? Encoding.UTF8 : Encoding.Unicode).GetString(data, p, length);
                }
            }
            else if (U16(position) == 0x102 && header >= 16 && size >= header + 20 && String(U32(position + header + 4)) == "manifest")
            {
                int attributes = position + header + U16(position + header + 8), stride = U16(position + header + 10), count = U16(position + header + 12);
                if (stride < 20 || attributes < position || attributes + (long)count * stride > position + size) throw new IOException("Invalid Android manifest attributes.");
                string package = ""; uint low = 0, high = 0;
                for (int i = 0; i < count; i++)
                {
                    int a = attributes + i * stride; string name = String(U32(a + 4)); byte type = data[a + 15]; uint value = U32(a + 16);
                    if (name == "package") package = String(type == 3 ? value : U32(a + 8));
                    else if (name == "versionCode" && type is 16 or 17) low = value;
                    else if (name == "versionCodeMajor" && type is 16 or 17) high = value;
                }
                if (package.Length == 0 || low == 0 || high > int.MaxValue) throw new IOException("The APK package/version could not be read.");
                return new(package, ((long)high << 32) | low);
            }
            position += size;
        }
        throw new IOException("The Android package identity is missing.");
    }
}
