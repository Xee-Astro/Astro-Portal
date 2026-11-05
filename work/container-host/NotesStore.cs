using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AsiairContainer;

public sealed record AppNotes(string Ip = "", string Password = "", string Notes = "");
public sealed record NotesData(AppNotes Asiair, AppNotes Stellavita);
public static class NotesStore
{
    private sealed record StoredEntry(string Ip, string ProtectedPassword, string Notes);
    private sealed record StoredNotes(int Version, StoredEntry Asiair, StoredEntry Stellavita);
    public static NotesData Load(string root)
    {
        string path = Path.Combine(root, "private-data", "notes.json");
        if (!File.Exists(path)) return new(new(), new());
        var data = JsonSerializer.Deserialize<StoredNotes>(File.ReadAllText(path)) ?? throw new IOException("Invalid notes file.");
        if (data.Version != 1) throw new IOException("Unsupported notes version.");
        AppNotes Read(StoredEntry e) => new(e.Ip, e.ProtectedPassword.Length == 0 ? "" : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(e.ProtectedPassword), null, DataProtectionScope.CurrentUser)), e.Notes);
        return new(Read(data.Asiair), Read(data.Stellavita));
    }
    public static void Save(string root, NotesData data)
    {
        StoredEntry Write(AppNotes e) => new(e.Ip, e.Password.Length == 0 ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(e.Password), null, DataProtectionScope.CurrentUser)), e.Notes);
        string path = Path.Combine(root, "private-data", "notes.json"); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new StoredNotes(1, Write(data.Asiair), Write(data.Stellavita)), new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}
