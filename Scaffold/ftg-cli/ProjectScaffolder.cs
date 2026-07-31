#nullable enable
namespace FTG_Framework.Scaffold;

internal sealed record ScaffoldHooks(
    Action<string>? Restore = null,
    Action<string>? BeforePlaceholderReplacement = null);

internal sealed class ScaffoldArtifactTracker
{
    private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    internal IReadOnlyCollection<string> Files => _files;
    internal IReadOnlyCollection<string> Directories => _directories;

    internal void EnsureDirectory(string path)
    {
        if (Directory.Exists(path))
            return;

        var missing = new Stack<string>();
        var current = Path.GetFullPath(path);
        while (!Directory.Exists(current))
        {
            missing.Push(current);
            current = Directory.GetParent(current)?.FullName
                ?? throw new InvalidOperationException($"Cannot resolve parent directory for {path}.");
        }

        Directory.CreateDirectory(path);
        foreach (var directory in missing)
            _directories.Add(directory);
    }

    internal Stream CreateFile(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        _files.Add(fullPath);
        return stream;
    }

    internal void ReplaceTrackedFile(string oldPath, string newPath)
    {
        _files.Remove(Path.GetFullPath(oldPath));
        _files.Add(Path.GetFullPath(newPath));
    }
}

public static class ProjectScaffolder
{
    private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".uid" };

    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", ".godot" };

    public static void Scaffold(string repoRoot, string targetDir, string projectName)
        => Scaffold(repoRoot, targetDir, projectName, new ScaffoldArtifactTracker(), new ScaffoldHooks());

    internal static void Scaffold(
        string repoRoot,
        string targetDir,
        string projectName,
        ScaffoldArtifactTracker artifacts,
        ScaffoldHooks hooks)
    {
        var templateDir = Path.Combine(repoRoot, "Scaffold", "ftg-project-template");
        var sourceDirs = ValidateInputs(repoRoot, templateDir);

        // 1. Copy template files
        CopyDirectory(templateDir, targetDir, artifacts);

        // 2. Rename .csproj (skip when the project name matches the template name)
        var oldCsproj = Path.Combine(targetDir, "FTG_Game.csproj");
        var newCsproj = Path.Combine(targetDir, $"{projectName}.csproj");
        if (!File.Exists(oldCsproj))
            throw new FileNotFoundException($"Template .csproj not found: {oldCsproj}");
        if (!string.Equals(oldCsproj, newCsproj, StringComparison.OrdinalIgnoreCase))
        {
            File.Move(oldCsproj, newCsproj);
            artifacts.ReplaceTrackedFile(oldCsproj, newCsproj);
        }

        // 3. Replace placeholders in project.godot and .csproj
        hooks.BeforePlaceholderReplacement?.Invoke(targetDir);
        ReplacePlaceholders(targetDir, projectName);

        // 4. Copy framework source
        var targetScriptsDir = Path.Combine(targetDir, "Scripts");
        artifacts.EnsureDirectory(targetScriptsDir);

        foreach (var srcDir in sourceDirs)
        {
            var sourceFullPath = Path.Combine(repoRoot, srcDir);
            var targetFullPath = Path.Combine(targetDir, srcDir);

            if (!Directory.Exists(sourceFullPath))
                throw new DirectoryNotFoundException($"Framework source directory not found: {sourceFullPath}");
            CopyDirectory(sourceFullPath, targetFullPath, artifacts);
        }

        // 5. Copy FrameRateManager.cs
        var frameRateManagerSrc = Path.Combine(repoRoot, "Scripts", "FrameRateManager.cs");
        var frameRateManagerDst = Path.Combine(targetDir, "Scripts", "FrameRateManager.cs");
        CopyFile(frameRateManagerSrc, frameRateManagerDst, artifacts);

        // 5.5 Copy Characters/ directory (scene template .tscn)
        var charactersSrc = Path.Combine(repoRoot, "Characters");
        var charactersDst = Path.Combine(targetDir, "Characters");
        CopyDirectory(charactersSrc, charactersDst, artifacts);

        // 6. Run dotnet restore
        RunDotnetRestore(targetDir, hooks);
    }

    private static string[] ValidateInputs(string repoRoot, string templateDir)
    {
        if (!Directory.Exists(templateDir))
            throw new DirectoryNotFoundException($"Project template directory not found: {templateDir}");

        foreach (var relativePath in new[] { "FTG_Game.csproj", "project.godot", "main.tscn", "global.json" })
        {
            var path = Path.Combine(templateDir, relativePath);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Required template file not found: {path}");
        }

        var sourceDirs = LoadFrameworkSourceDirs(repoRoot);
        foreach (var sourceDir in sourceDirs)
        {
            var path = Path.Combine(repoRoot, sourceDir);
            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException($"Framework source directory not found: {path}");
        }

        var requiredFiles = new[]
        {
            Path.Combine("Scripts", "FrameRateManager.cs"),
            Path.Combine("Characters", "character_template.tscn"),
            Path.Combine("Scripts", "Framework", "Data", "example_moves.json"),
            Path.Combine("Scripts", "Framework", "Data", "example_characters.json"),
            Path.Combine("Scripts", "Framework", "Data", "example_gatling.json"),
            Path.Combine("Scripts", "Framework", "Data", "example_knockback_profiles.json"),
            Path.Combine("Scripts", "Framework", "Data", "template_fighter.json"),
        };
        foreach (var relativePath in requiredFiles)
        {
            var path = Path.Combine(repoRoot, relativePath);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Required scaffold input not found: {path}");
        }

        return sourceDirs;
    }

    private static string[] LoadFrameworkSourceDirs(string repoRoot)
    {
        var manifestPath = Path.Combine(repoRoot, "Scaffold", "framework-source-dirs.txt");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Framework source manifest not found: {manifestPath}");

        var dirs = File.ReadAllLines(manifestPath)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Replace('\\', '/'))
            .ToArray();
        if (dirs.Length == 0)
            throw new InvalidOperationException($"Framework source manifest is empty: {manifestPath}");
        return dirs;
    }

    private static void ReplacePlaceholders(string targetDir, string projectName)
    {
        // project.godot
        var godotPath = Path.Combine(targetDir, "project.godot");
        if (!File.Exists(godotPath))
            throw new FileNotFoundException($"Template project.godot not found: {godotPath}");
        var godotContent = File.ReadAllText(godotPath);
        godotContent = godotContent.Replace("{{FTG_PROJECT_NAME}}", projectName);
        File.WriteAllText(godotPath, godotContent);

        // .csproj (already renamed)
        var csprojPath = Path.Combine(targetDir, $"{projectName}.csproj");
        if (!File.Exists(csprojPath))
            throw new FileNotFoundException($"Renamed .csproj not found: {csprojPath}");
        var csprojContent = File.ReadAllText(csprojPath);
        csprojContent = csprojContent.Replace("{{FTG_PROJECT_NAME}}", projectName);
        File.WriteAllText(csprojPath, csprojContent);
    }

    private static void CopyDirectory(string sourceDir, string targetDir, ScaffoldArtifactTracker artifacts)
    {
        artifacts.EnsureDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var ext = Path.GetExtension(file);
            if (ExcludedExtensions.Contains(ext))
                continue;

            var destFile = Path.Combine(targetDir, Path.GetFileName(file));
            CopyFile(file, destFile, artifacts);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            // Skip junction/symlink loops — recursion into reparse points is unbounded
            if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0)
                continue;

            var dirName = Path.GetFileName(dir);
            if (ExcludedDirectories.Contains(dirName))
                continue;

            var destDir = Path.Combine(targetDir, dirName);
            CopyDirectory(dir, destDir, artifacts);
        }
    }

    private static void CopyFile(string sourcePath, string destinationPath, ScaffoldArtifactTracker artifacts)
    {
        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var destination = artifacts.CreateFile(destinationPath);
        source.CopyTo(destination);
    }

    // Restore output flows to the console (no redirected pipes to deadlock).
    // A generous timeout guards against hung NuGet servers; first-time restores
    // can take minutes, but five minutes is enough for any reasonable connection.
    private static void RunDotnetRestore(string targetDir, ScaffoldHooks hooks)
    {
        if (hooks.Restore is not null)
        {
            hooks.Restore(targetDir);
            return;
        }

        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "restore",
            WorkingDirectory = targetDir,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("[Scaffold] Could not start dotnet restore.");

        if (!process.WaitForExit(300_000))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            if (!process.WaitForExit(10_000))
                throw new TimeoutException("[Scaffold] dotnet restore timed out and did not exit after termination.");
            throw new TimeoutException("[Scaffold] dotnet restore timed out after 5 minutes.");
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"[Scaffold] dotnet restore exited with code {process.ExitCode}.");
    }
}
