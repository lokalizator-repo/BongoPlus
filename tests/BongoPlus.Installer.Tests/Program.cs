using System.IO.Compression;
using System.Runtime.Serialization.Json;
using System.Text;
using BongoPlusInstaller;

var checks = 0;
void Check(bool valid, string message)
{
    if (!valid) throw new InvalidOperationException(message);
    Console.WriteLine("PASS " + message);
    checks++;
}
bool Rejects(Action action)
{
    try { action(); return false; }
    catch (InvalidOperationException) { return true; }
    catch (InvalidDataException) { return true; }
    catch (IOException) { return true; }
}
MemoryStream Package(string version, string? extra = null)
{
    var stream = new MemoryStream();
    using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        foreach (var path in new[] { "winhttp.dll", "doorstop_config.ini", InstallEngine.PluginPath, "BepInEx/config/BepInEx.cfg", "BepInEx/core/BepInEx.dll",
            "BepInEx/plugins/BongoCatMods/legal/LICENSE.txt" }.Concat(extra == null ? Array.Empty<string>() : new[] { extra }))
        {
            using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
            writer.Write(path == "BepInEx/config/BepInEx.cfg" ? "[Logging]\nUnityLogListening = false\n" : path == "winhttp.dll" ? "loader" : version);
        }
    stream.Position = 0;
    return stream;
}
var root = Path.Combine(Path.GetTempPath(), "BongoPlus-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var game = Path.Combine(root, "Steam library", "Котик", "BongoCat");
Directory.CreateDirectory(Path.Combine(game, "BongoCat_Data", "Managed"));
File.WriteAllText(Path.Combine(game, "BongoCat.exe"), "fixture");
File.WriteAllText(Path.Combine(game, "BongoCat_Data", "Managed", "Assembly-CSharp.dll"), "fixture");
string FileAt(string path) => Path.Combine(game, path.Replace('/', Path.DirectorySeparatorChar));
try
{
    Check(SteamDiscovery.LibraryPaths("\"libraryfolders\" { \"0\" { \"path\" \"D:\\\\Steam Library\" } \"1\" \"E:\\\\Games\" }")
        .SequenceEqual(new[] { @"D:\Steam Library", @"E:\Games" }), "Steam libraries support current and legacy VDF formats, spaces and escaped paths");
    Check(SteamDiscovery.Value("\"AppState\" { \"installdir\" \"BongoCat\" }", "installdir") == "BongoCat", "Steam app manifest supplies the installation folder");
    Check(InstallEngine.ValidateGame(game) == game && !InstallEngine.KnownBuild(game), "Unicode game folders and unrecognized builds are accepted");
    Check(Rejects(() => InstallEngine.ValidateGame(root)), "A folder without the game cannot be installed into");
    using var initialZip = Package("v1");
    var initial = new InstallEngine(initialZip);
    initial.Install(game, (_, _) => { });
    Check(InstallEngine.Installed(game) && InstallEngine.ReadManifest(game)?.Version == InstallEngine.Version, "Fresh installation writes plugin and ownership record");
    Check(File.ReadAllText(FileAt("BongoCat.exe")) == "fixture" && File.ReadAllText(FileAt("BongoCat_Data/Managed/Assembly-CSharp.dll")) == "fixture", "Original game files remain unchanged");
    var settings = FileAt("BepInEx/config/local.bongocat.mods.cfg");
    File.WriteAllText(settings, "Котик=993");
    var loaderConfig = FileAt("BepInEx/config/BepInEx.cfg");
    File.WriteAllText(loaderConfig, "[Logging]\nUnityLogListening = true\n[Other]\nKeep = yes\n");
    using var updatedZip = Package("v2");
    var update = new InstallEngine(updatedZip);
    update.Install(game, (_, _) => { });
    Check(File.ReadAllText(FileAt(InstallEngine.PluginPath)) == "v2" && File.ReadAllText(settings) == "Котик=993", "Updates replace the plugin while preserving user settings");
    Check(File.ReadAllText(loaderConfig).Contains("UnityLogListening = false") && File.ReadAllText(loaderConfig).Contains("Keep = yes"), "Loader configuration retains unrelated options");
    var marker = FileAt(InstallEngine.MarkerPath);
    var legacy = FileAt("BepInEx/config/local.bongocat.mods.install.json");
    File.Copy(marker, legacy);
    File.Delete(marker);
    update.Install(game, (_, _) => { });
    Check(File.Exists(marker) && !File.Exists(legacy), "Existing 0.x ownership records migrate without losing settings");
    var beforePlugin = File.ReadAllText(FileAt(InstallEngine.PluginPath));
    Check(Rejects(() => initial.Install(game, (_, _) => throw new IOException("Simulated write failure"))) &&
        File.ReadAllText(FileAt(InstallEngine.PluginPath)) == beforePlugin && InstallEngine.ReadManifest(game)?.Version == InstallEngine.Version,
        "A failed operation rolls back changed files and the installation record");
    File.WriteAllText(FileAt(InstallEngine.PluginPath), "modified plugin");
    Check(Rejects(() => update.Install(game, (_, _) => { })) && File.ReadAllText(FileAt(InstallEngine.PluginPath)) == "modified plugin", "Updates refuse to overwrite modified or foreign files");
    var message = update.Uninstall(game, (_, _) => { });
    Check(InstallEngine.Installed(game) && message.Contains("modified"), "Uninstall retains a modified plugin and explains why");
    File.WriteAllText(FileAt(InstallEngine.PluginPath), "v2");
    update.Install(game, (_, _) => { });
    var foreign = FileAt("BepInEx/plugins/OtherMod.dll");
    File.WriteAllText(foreign, "foreign");
    update.Uninstall(game, (_, _) => { });
    Check(!InstallEngine.Installed(game) && File.Exists(FileAt("winhttp.dll")) && File.ReadAllText(foreign) == "foreign", "Removing BongoPlus keeps the loader used by other plugins");
    File.Delete(foreign);
    update.Uninstall(game, (_, _) => { });
    Check(!File.Exists(FileAt("winhttp.dll")) && File.Exists(settings) && File.Exists(loaderConfig), "Complete removal keeps configuration while removing owned loader files");
    update.Install(game, (_, _) => { });
    File.WriteAllText(FileAt("winhttp.dll"), "modified loader");
    update.Uninstall(game, (_, _) => { });
    Check(!InstallEngine.Installed(game) && File.Exists(FileAt("BepInEx/core/BepInEx.dll")) && File.ReadAllText(FileAt("winhttp.dll")) == "modified loader",
        "A modified shared loader keeps its dependencies during removal");
    File.WriteAllText(FileAt("winhttp.dll"), "loader");
    update.Uninstall(game, (_, _) => { });
    File.WriteAllText(FileAt("winhttp.dll"), "unknown loader");
    Check(Rejects(() => update.Install(game, (_, _) => { })) && File.ReadAllText(FileAt("winhttp.dll")) == "unknown loader", "An unknown loader is never replaced automatically");
    File.Delete(FileAt("winhttp.dll"));
    foreach (var invalid in new[] { "../escape.dll", "BepInEx/core/../../escape.dll", "C:/escape.dll", "BongoCat.exe", "BepInEx/plugins/Other.dll",
        "BepInEx/plugins/BongoCatMods/legal/hidden.dll" })
    {
        using var zip = Package("bad", invalid);
        Check(Rejects(() => new InstallEngine(zip)), "Package rejects unexpected or escaping path: " + invalid);
    }
    Check(Rejects(() => InstallEngine.Target(game, "../outside")), "Installation record paths cannot escape the game folder");
    using (var zip = Package("v1"))
    {
        var installer = new InstallEngine(zip);
        installer.Install(game, (_, _) => { });
        var manifest = InstallEngine.ReadManifest(game)!;
        manifest.Files.Add(new InstallRecord { Path = "../outside", Hash = new string('a', 64) });
        using (var output = File.Create(marker)) new DataContractJsonSerializer(typeof(InstallManifest)).WriteObject(output, manifest);
        Check(Rejects(() => installer.Uninstall(game, (_, _) => { })) && InstallEngine.Installed(game), "A malformed ownership record cannot drive file deletion");
    }
    if (args.Length == 1)
    {
        var productionGame = Path.Combine(root, "production-package");
        Directory.CreateDirectory(Path.Combine(productionGame, "BongoCat_Data", "Managed"));
        File.WriteAllText(Path.Combine(productionGame, "BongoCat.exe"), "fixture");
        File.WriteAllText(Path.Combine(productionGame, "BongoCat_Data", "Managed", "Assembly-CSharp.dll"), "fixture");
        Directory.CreateDirectory(Path.Combine(productionGame, "BepInEx", "config"));
        var savedConfig = Path.Combine(productionGame, "BepInEx", "config", "local.bongocat.mods.cfg");
        File.WriteAllText(savedConfig, "Котик=993");
        var installerAssembly = System.Reflection.Assembly.LoadFile(Path.GetFullPath(args[0]));
        using var embedded = installerAssembly.GetManifestResourceStream("BongoPlus.Payload.zip") ?? throw new InvalidDataException("Missing embedded package");
        var packaged = new InstallEngine(embedded);
        packaged.Install(productionGame, (_, _) => { });
        var installed = InstallEngine.ReadManifest(productionGame)!;
        Check(installed.Files.Count > 20 && new FileInfo(InstallEngine.Target(productionGame, InstallEngine.PluginPath)).Length > 50000,
            "The portable EXE contains the full production plugin and loader package");
        var legal = Path.Combine(productionGame, "BepInEx", "plugins", "BongoCatMods", "legal");
        Check(File.ReadAllText(Path.Combine(legal, "LICENSE.txt")).Contains("MIT License") &&
            File.Exists(Path.Combine(legal, "THIRD_PARTY_NOTICES.md")) && File.Exists(Path.Combine(legal, "THIRD_PARTY_MANIFEST.json")) &&
            Directory.GetFiles(Path.Combine(legal, "licenses"), "*.txt").Length >= 8,
            "The distributed package installs its license, third-party notices and binary inventory");
        using (var source = ZipFile.OpenRead(Path.Combine(legal, "sources", "UnityDoorstop-4.5.0-source.zip")))
            Check(source.Entries.Any(entry => entry.FullName.EndsWith("/LICENSE")) &&
                source.Entries.Any(entry => entry.FullName.EndsWith("/build.ps1")) &&
                source.Entries.Any(entry => entry.FullName.Contains("/src/windows/")),
                "The package includes UnityDoorstop source code and its build scripts");
        packaged.Uninstall(productionGame, (_, _) => { });
        Check(!InstallEngine.Installed(productionGame) && File.ReadAllText(savedConfig) == "Котик=993", "The embedded production package installs and uninstalls while preserving settings");
    }
    Console.WriteLine($"{checks} installer checks passed.");
}
finally
{
    var verified = Path.GetFullPath(root);
    if (!verified.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(verified).StartsWith("BongoPlus-tests-"))
        throw new InvalidOperationException("Unexpected test cleanup path.");
    Directory.Delete(verified, true);
}
