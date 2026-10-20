using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace BongoPlusInstaller;

[DataContract]
internal sealed class InstallRecord
{
    [DataMember(Name = "path")] public string Path = "";
    [DataMember(Name = "sha256")] public string Hash = "";
}

[DataContract]
internal sealed class InstallManifest
{
    [DataMember(Name = "plugin")] public string Version = "";
    [DataMember(Name = "files")] public List<InstallRecord> Files = new List<InstallRecord>();
}

internal sealed class InstallEngine
{
    internal const string Version = "1.0.0";
    internal const string PluginPath = "BepInEx/plugins/BongoCatMods/BongoCat.Mod.dll";
    internal const string MarkerPath = "BepInEx/config/bongoplus.install.json";
    private const string LegacyMarker = "BepInEx/config/local.bongocat.mods.install.json";
    private const string ConfigPath = "BepInEx/config/BepInEx.cfg";
    private const string KnownAssembly = "96bbcbb2733988fd31ce261891eb3027888389e03b6a23dbd63e9e7f518adfb8";
    private readonly Dictionary<string, byte[]> _payload;

    internal InstallEngine(Stream archive)
    {
        _payload = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using var zip = new ZipArchive(archive, ZipArchiveMode.Read, true);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            var path = Normalize(entry.FullName);
            if (!Allowed(path) || _payload.ContainsKey(path)) throw new InvalidDataException("Unexpected package entry: " + path);
            using var input = entry.Open();
            using var output = new MemoryStream();
            input.CopyTo(output);
            _payload.Add(path, output.ToArray());
        }
        foreach (var required in new[] { "winhttp.dll", "doorstop_config.ini", PluginPath, ConfigPath })
            if (!_payload.ContainsKey(required)) throw new InvalidDataException("Incomplete installer package: " + required);
    }

    internal static string ValidateGame(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Choose the Bongo Cat game folder.");
        var root = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!File.Exists(Target(root, "BongoCat.exe")) ||
            !File.Exists(Target(root, "BongoCat_Data/Managed/Assembly-CSharp.dll")))
            throw new InvalidOperationException("This folder does not contain the Windows Mono version of Bongo Cat.");
        return root;
    }

    internal static bool KnownBuild(string root) => Hash(File.ReadAllBytes(Target(root, "BongoCat_Data/Managed/Assembly-CSharp.dll"))) == KnownAssembly;
    internal static bool Installed(string root) => File.Exists(Target(root, PluginPath));

    internal static InstallManifest? ReadManifest(string root)
    {
        var path = Target(root, MarkerPath);
        if (!File.Exists(path)) path = Target(root, LegacyMarker);
        if (!File.Exists(path)) return null;
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(path)));
            var manifest = (InstallManifest?)new DataContractJsonSerializer(typeof(InstallManifest)).ReadObject(stream);
            if (manifest?.Files == null || manifest.Files.Any(record => record == null || !Allowed(Normalize(record.Path)) ||
                record.Hash == null || !System.Text.RegularExpressions.Regex.IsMatch(record.Hash, "^[0-9a-fA-F]{64}$")))
                throw new InvalidDataException("Invalid installation record.");
            if (manifest.Files.Select(record => Normalize(record.Path)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Files.Count)
                throw new InvalidDataException("Duplicate installation record.");
            return manifest;
        }
        catch (SerializationException error) { throw new InvalidDataException("The installation record is damaged. Restore it before updating.", error); }
    }

    internal void Install(string directory, Action<int, string> progress)
    {
        var root = ValidateGame(directory);
        EnsureClosed();
        var previous = ReadManifest(root);
        var owned = (previous?.Files ?? new List<InstallRecord>()).ToDictionary(record => Normalize(record.Path), record => record.Hash,
            StringComparer.OrdinalIgnoreCase);
        if (File.Exists(Target(root, "winhttp.dll")) && !owned.ContainsKey("winhttp.dll"))
            throw new InvalidOperationException("Another loader is already installed. BongoPlus will not replace it automatically.");
        var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        var records = new List<InstallRecord>();
        foreach (var entry in _payload)
        {
            var path = Target(root, entry.Key);
            var exists = File.Exists(path);
            if (entry.Key.Equals(ConfigPath, StringComparison.OrdinalIgnoreCase) && exists)
            {
                var content = File.ReadAllText(path);
                var updated = ConfigureLoader(content);
                if (content != updated) changes.Add(entry.Key, new UTF8Encoding(false).GetBytes(updated));
                continue;
            }
            if (exists && Hash(File.ReadAllBytes(path)) != Hash(entry.Value) &&
                (!owned.TryGetValue(entry.Key, out var expected) || !HashMatches(path, expected)))
                throw new InvalidOperationException("A file belongs to another installation or was modified:\n" + entry.Key + "\nBongoPlus has left it unchanged.");
            changes.Add(entry.Key, entry.Value);
            records.Add(new InstallRecord { Path = entry.Key, Hash = Hash(entry.Value) });
        }
        foreach (var record in previous?.Files ?? new List<InstallRecord>())
        {
            var relative = Normalize(record.Path);
            if (!_payload.ContainsKey(relative)) records.Add(record);
        }
        using var output = new MemoryStream();
        new DataContractJsonSerializer(typeof(InstallManifest)).WriteObject(output, new InstallManifest { Version = Version, Files = records });
        changes.Add(MarkerPath, output.ToArray());
        if (File.Exists(Target(root, LegacyMarker))) changes.Add(LegacyMarker, null);
        Apply(root, changes, progress);
    }

    internal string Uninstall(string directory, Action<int, string> progress)
    {
        var root = ValidateGame(directory);
        EnsureClosed();
        var manifest = ReadManifest(root) ?? throw new InvalidOperationException("No BongoPlus installation record was found. Files were left unchanged.");
        var others = OtherPlugins(root);
        var sharedChanged = manifest.Files.Any(record =>
            !Normalize(record.Path).Equals(PluginPath, StringComparison.OrdinalIgnoreCase) &&
            !Normalize(record.Path).Equals(ConfigPath, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(Target(root, record.Path)) && !HashMatches(Target(root, record.Path), record.Hash));
        var unknownLoader = File.Exists(Target(root, "winhttp.dll")) &&
            !manifest.Files.Any(record => Normalize(record.Path).Equals("winhttp.dll", StringComparison.OrdinalIgnoreCase));
        var keepShared = others || sharedChanged || unknownLoader;
        var retained = new List<InstallRecord>();
        var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in manifest.Files.OrderByDescending(record => Normalize(record.Path).Equals(PluginPath, StringComparison.OrdinalIgnoreCase)))
        {
            var relative = Normalize(record.Path);
            var path = Target(root, relative);
            if (!File.Exists(path)) continue;
            if (relative.Equals(ConfigPath, StringComparison.OrdinalIgnoreCase) ||
                (keepShared && !relative.Equals(PluginPath, StringComparison.OrdinalIgnoreCase)) || !HashMatches(path, record.Hash))
            { retained.Add(record); continue; }
            changes.Add(relative, null);
        }
        if (retained.Count == 0) changes.Add(MarkerPath, null);
        else
        {
            using var stream = new MemoryStream();
            new DataContractJsonSerializer(typeof(InstallManifest)).WriteObject(stream, new InstallManifest { Version = manifest.Version, Files = retained });
            changes.Add(MarkerPath, stream.ToArray());
        }
        if (File.Exists(Target(root, LegacyMarker))) changes.Add(LegacyMarker, null);
        Apply(root, changes, progress);
        if (Installed(root)) return "The plugin file was modified and has been kept. Remove it manually if you no longer need it.";
        return others ? "BongoPlus removed. Settings and the loader used by other plugins were kept." :
            retained.Count > 0 ? "BongoPlus removed. Settings and modified shared files were kept." : "BongoPlus removed. Your settings were kept.";
    }

    internal static bool OtherPlugins(string root)
    {
        foreach (var folder in new[] { "BepInEx/plugins", "BepInEx/patchers" })
        {
            var path = Target(root, folder);
            if (!Directory.Exists(path)) continue;
            if (DllFiles(root, path).Any(file => !file.Equals(Target(root, PluginPath), StringComparison.OrdinalIgnoreCase))) return true;
        }
        return false;
    }

    private static IEnumerable<string> DllFiles(string root, string directory)
    {
        foreach (var file in Directory.GetFiles(directory, "*.dll"))
            yield return Target(root, file.Substring(root.Length + 1));
        foreach (var child in Directory.GetDirectories(directory))
        {
            Target(root, child.Substring(root.Length + 1));
            foreach (var file in DllFiles(root, child)) yield return file;
        }
    }

    internal static void EnsureClosed()
    {
        var processes = Process.GetProcessesByName("BongoCat");
        try { if (processes.Length > 0) throw new InvalidOperationException("Close Bongo Cat, then try again. Your game will not be closed automatically."); }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    internal static string Target(string root, string relative)
    {
        var normalized = Normalize(relative);
        var target = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Path escapes the game folder.");
        var current = target;
        while (current != null)
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked folders and files are not supported: " + current);
            current = Path.GetDirectoryName(current);
        }
        return target;
    }

    private static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("Empty package path.");
        var value = path.Replace('\\', '/');
        if (value.StartsWith("/", StringComparison.Ordinal) || value.IndexOf(':') >= 0 ||
            value.Split('/').Any(part => part == ".." || part == "." || part.Length == 0 || part.EndsWith(".", StringComparison.Ordinal) || part.EndsWith(" ", StringComparison.Ordinal)))
            throw new InvalidDataException("Invalid package path: " + path);
        return value;
    }

    private static bool Allowed(string path) => path.Equals("winhttp.dll", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("doorstop_config.ini", StringComparison.OrdinalIgnoreCase) || path.Equals(ConfigPath, StringComparison.OrdinalIgnoreCase) ||
        path.Equals(PluginPath, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("BepInEx/plugins/BongoCatMods/legal/", StringComparison.OrdinalIgnoreCase) &&
        (path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
         path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) ||
        (path.StartsWith("BepInEx/core/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("BepInEx/unstripped_corlib/", StringComparison.OrdinalIgnoreCase)) &&
        (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

    private static bool HashMatches(string path, string expected) => Hash(File.ReadAllBytes(path)).Equals(expected, StringComparison.OrdinalIgnoreCase);
    private static string Hash(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
    }

    private static string ConfigureLoader(string text)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"(?m)^UnityLogListening\s*="))
            return System.Text.RegularExpressions.Regex.Replace(text, @"(?m)^UnityLogListening\s*=.*$", "UnityLogListening = false");
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"(?m)^\[Logging\]\s*$"))
            return System.Text.RegularExpressions.Regex.Replace(text, @"(?m)^\[Logging\]\s*$", "[Logging]\r\nUnityLogListening = false\r\n");
        return text + "\r\n[Logging]\r\nUnityLogListening = false\r\n";
    }

    private static void Apply(string root, Dictionary<string, byte[]?> changes, Action<int, string> progress)
    {
        var backups = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        var created = new List<string>();
        foreach (var entry in changes)
        {
            var target = Target(root, entry.Key);
            backups.Add(entry.Key, File.Exists(target) ? File.ReadAllBytes(target) : null);
            var parent = Path.GetDirectoryName(target)!;
            while (!Directory.Exists(parent) && !created.Contains(parent)) { created.Add(parent); parent = Path.GetDirectoryName(parent)!; }
        }
        try
        {
            var completed = 0;
            foreach (var entry in changes)
            {
                var target = Target(root, entry.Key);
                if (entry.Value == null) File.Delete(target);
                else { Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.WriteAllBytes(target, entry.Value); }
                progress(++completed * 100 / changes.Count, entry.Key);
            }
        }
        catch (Exception error)
        {
            var failures = new List<Exception> { error };
            foreach (var entry in backups.Reverse())
            {
                try
                {
                    var target = Target(root, entry.Key);
                    if (entry.Value == null) File.Delete(target);
                    else { Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.WriteAllBytes(target, entry.Value); }
                }
                catch (Exception rollback) { failures.Add(rollback); }
            }
            if (failures.Count > 1) throw new AggregateException("The operation failed and could not be fully rolled back. Keep the game closed and restore the affected files.", failures);
            throw;
        }
        finally
        {
            foreach (var directory in created.OrderByDescending(path => path.Length))
                try { if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
        }
    }
}
