using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BongoPlusInstaller;

internal static class SteamDiscovery
{
    internal const string AppId = "3419430";

    internal static IReadOnlyList<string> FindGames()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                try
                {
                    using var registry = RegistryKey.OpenBaseKey(hive, view);
                    using var key = registry.OpenSubKey(@"Software\Valve\Steam");
                    Add(roots, key?.GetValue("SteamPath") as string ?? key?.GetValue("InstallPath") as string);
                }
                catch (System.Security.SecurityException) { }
                catch (UnauthorizedAccessException) { }
            }
        Add(roots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        var libraries = new HashSet<string>(roots, StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            try
            {
                var file = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                if (File.Exists(file)) foreach (var path in LibraryPaths(File.ReadAllText(file))) Add(libraries, path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        var games = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var library in libraries)
        {
            try
            {
                var apps = Path.Combine(library, "steamapps");
                var manifest = Path.Combine(apps, "appmanifest_" + AppId + ".acf");
                if (File.Exists(manifest))
                {
                    var directory = Value(File.ReadAllText(manifest), "installdir");
                    if (!string.IsNullOrWhiteSpace(directory)) AddGame(games, Path.Combine(apps, "common", directory));
                }
                AddGame(games, Path.Combine(apps, "common", "BongoCat"));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { }
        }
        return games.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static IEnumerable<string> LibraryPaths(string text)
    {
        foreach (Match match in Regex.Matches(text, "\"(?:path|[0-9]+)\"\\s*\"((?:\\\\.|[^\"\\\\])*)\"", RegexOptions.IgnoreCase))
            yield return Unescape(match.Groups[1].Value);
    }

    internal static string? Value(string text, string key)
    {
        var match = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*\"((?:\\\\.|[^\"\\\\])*)\"", RegexOptions.IgnoreCase);
        return match.Success ? Unescape(match.Groups[1].Value) : null;
    }

    private static string Unescape(string value) => value.Replace("\\\\", "\\").Replace("\\\"", "\"");
    private static void Add(HashSet<string> paths, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try { if (Directory.Exists(path)) paths.Add(Path.GetFullPath(path)); }
        catch (ArgumentException) { }
    }
    private static void AddGame(HashSet<string> paths, string path)
    {
        if (File.Exists(Path.Combine(path, "BongoCat.exe")) &&
            File.Exists(Path.Combine(path, "BongoCat_Data", "Managed", "Assembly-CSharp.dll"))) Add(paths, path);
    }
}
