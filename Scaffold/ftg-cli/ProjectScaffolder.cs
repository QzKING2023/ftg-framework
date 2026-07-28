#nullable enable
namespace FTG_Framework.Scaffold;

public static class ProjectScaffolder
{
    private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".uid" };

    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", ".godot" };

    public static void Scaffold(string repoRoot, string targetDir, string projectName)
    {
        var templateDir = Path.Combine(repoRoot, "Scaffold", "ftg-project-template");

        // 1. Copy template files
        CopyDirectory(templateDir, targetDir);

        // 2. Rename .csproj (skip when the project name matches the template name)
        var oldCsproj = Path.Combine(targetDir, "FTG_Game.csproj");
        var newCsproj = Path.Combine(targetDir, $"{projectName}.csproj");
        if (!File.Exists(oldCsproj))
            throw new FileNotFoundException($"Template .csproj not found: {oldCsproj}");
        if (!string.Equals(oldCsproj, newCsproj, StringComparison.OrdinalIgnoreCase))
            File.Move(oldCsproj, newCsproj);

        // 3. Replace placeholders in project.godot and .csproj
        ReplacePlaceholders(targetDir, projectName);

        // 4. Copy framework source
        var targetScriptsDir = Path.Combine(targetDir, "Scripts");
        Directory.CreateDirectory(targetScriptsDir);

        foreach (var srcDir in LoadFrameworkSourceDirs(repoRoot))
        {
            var sourceFullPath = Path.Combine(repoRoot, srcDir);
            var targetFullPath = Path.Combine(targetDir, srcDir);

            if (!Directory.Exists(sourceFullPath))
                throw new DirectoryNotFoundException($"Framework source directory not found: {sourceFullPath}");
            CopyDirectory(sourceFullPath, targetFullPath);
        }

        // 5. Copy FrameRateManager.cs
        var frameRateManagerSrc = Path.Combine(repoRoot, "Scripts", "FrameRateManager.cs");
        var frameRateManagerDst = Path.Combine(targetDir, "Scripts", "FrameRateManager.cs");
        if (!File.Exists(frameRateManagerSrc))
            throw new FileNotFoundException($"FrameRateManager.cs not found: {frameRateManagerSrc}");
        File.Copy(frameRateManagerSrc, frameRateManagerDst, overwrite: true);

        // 6. Run dotnet restore
        RunDotnetRestore(targetDir);
    }

    private static string[] LoadFrameworkSourceDirs(string repoRoot)
    {
        var manifestPath = Path.Combine(repoRoot, "Scaffold", "framework-source-dirs.txt");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Framework source manifest not found: {manifestPath}");

        var dirs = File.ReadAllLines(manifestPath)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
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

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var ext = Path.GetExtension(file);
            if (ExcludedExtensions.Contains(ext))
                continue;

            var destFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
            // Make copied file writable (template files may be read-only from source control)
            File.SetAttributes(destFile, FileAttributes.Normal);
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
            CopyDirectory(dir, destDir);
        }
    }

    // Restore output flows to the console (no redirected pipes to deadlock);
    // there is no artificial timeout — first-time NuGet restores can take minutes.
    private static void RunDotnetRestore(string targetDir)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "restore",
                WorkingDirectory = targetDir,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null)
            {
                Console.WriteLine("[Scaffold] Warning: could not start dotnet restore. Run 'dotnet restore' manually.");
                return;
            }
            process.WaitForExit();
            if (process.ExitCode != 0)
                Console.WriteLine($"[Scaffold] Warning: dotnet restore exited with code {process.ExitCode}. Run 'dotnet restore' manually.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Scaffold] Warning: dotnet restore failed ({ex.Message}). Run 'dotnet restore' manually.");
        }
    }
}
