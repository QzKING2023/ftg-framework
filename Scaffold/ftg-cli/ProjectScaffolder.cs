#nullable enable
namespace FTG_Framework.Scaffold;

internal sealed record ScaffoldHooks(
    Action<string>? Restore = null,
    Action<string>? BeforePlaceholderReplacement = null,
    Action<string>? BeforeFileAccess = null);

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
        var plan = BuildPlan(repoRoot, targetDir, projectName, hooks);
        hooks.BeforePlaceholderReplacement?.Invoke(plan.TargetRoot);
        foreach (var directory in plan.Directories)
        {
            ValidateTargetAccess(plan.TargetRoot, directory);
            artifacts.EnsureDirectory(directory);
        }
        foreach (var entry in plan.Files)
            CopyPlannedFile(plan, entry, projectName, artifacts, hooks);

        ValidateTargetTree(plan.TargetRoot);
        RunDotnetRestore(plan.TargetRoot, hooks);
    }

    private sealed record PlannedFile(string Source, string Destination, bool ReplacePlaceholders);
    private sealed record ScaffoldPlan(
        string RepoRoot, string TargetRoot, IReadOnlyList<string> Directories, IReadOnlyList<PlannedFile> Files);

    private static ScaffoldPlan BuildPlan(
        string repoRoot, string targetDir, string projectName, ScaffoldHooks hooks)
    {
        var projectNameError = Program.ValidateProjectName(projectName);
        if (projectNameError is not null)
            throw new ArgumentException(projectNameError, nameof(projectName));

        var root = Path.GetFullPath(repoRoot);
        var target = Path.GetFullPath(targetDir);
        ValidateExistingPathChain(root, root);
        ValidateTargetAccess(target, target);

        var templateDir = Path.Combine(root, "Scaffold", "ftg-project-template");
        var sourceDirs = ValidateInputs(root, templateDir, hooks);
        var files = new List<PlannedFile>();
        var directories = new List<string>();
        AddTree(root, target, templateDir, target, projectName, directories, files);
        foreach (var sourceDir in sourceDirs)
            AddTree(root, target, Path.Combine(root, sourceDir), Path.Combine(target, sourceDir), projectName, directories, files);
        AddFile(root, target, Path.Combine(root, "Scripts", "FrameRateManager.cs"),
            Path.Combine(target, "Scripts", "FrameRateManager.cs"), projectName, files);
        AddTree(root, target, Path.Combine(root, "Characters"), Path.Combine(target, "Characters"), projectName, directories, files);

        var duplicate = files.GroupBy(entry => entry.Destination, PathComparer)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Scaffold plan has duplicate destination: {duplicate.Key}");
        return new ScaffoldPlan(root, target, directories.Distinct(PathComparer).ToArray(), files.ToArray());
    }

    private static void AddTree(string root, string targetRoot, string sourceDir, string destinationDir,
        string projectName, List<string> directories, List<PlannedFile> files)
    {
        ValidateExistingPathChain(root, sourceDir);
        destinationDir = Path.GetFullPath(destinationDir);
        EnsureContained(targetRoot, destinationDir, "destination directory");
        ValidateTargetAccess(targetRoot, destinationDir);
        directories.Add(destinationDir);
        foreach (var file in Directory.EnumerateFiles(sourceDir).OrderBy(path => path, StringComparer.Ordinal))
        {
            if (!ExcludedExtensions.Contains(Path.GetExtension(file)))
                AddFile(root, targetRoot, file, Path.Combine(destinationDir, Path.GetFileName(file)), projectName, files);
        }
        foreach (var directory in Directory.EnumerateDirectories(sourceDir).OrderBy(path => path, StringComparer.Ordinal))
        {
            ValidateExistingPathChain(root, directory);
            if (ExcludedDirectories.Contains(Path.GetFileName(directory)))
                continue;
            AddTree(root, targetRoot, directory, Path.Combine(destinationDir, Path.GetFileName(directory)), projectName, directories, files);
        }
    }

    private static void AddFile(string root, string targetRoot, string source, string destination,
        string projectName, List<PlannedFile> files)
    {
        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);
        ValidateExistingPathChain(root, source);
        EnsureContained(targetRoot, destination, "destination");
        ValidateTargetAccess(targetRoot, destination);
        if (string.Equals(Path.GetFileName(source), "FTG_Game.csproj", StringComparison.OrdinalIgnoreCase))
        {
            destination = Path.Combine(Path.GetDirectoryName(destination)!, $"{projectName}.csproj");
            destination = Path.GetFullPath(destination);
            EnsureContained(targetRoot, destination, "renamed project destination");
            ValidateTargetAccess(targetRoot, destination);
        }
        var replace = string.Equals(Path.GetFileName(source), "project.godot", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(Path.GetFileName(source), "FTG_Game.csproj", StringComparison.OrdinalIgnoreCase);
        files.Add(new PlannedFile(source, destination, replace));
    }

    private static string[] ValidateInputs(string repoRoot, string templateDir, ScaffoldHooks hooks)
    {
        if (!Directory.Exists(templateDir))
            throw new DirectoryNotFoundException($"Project template directory not found: {templateDir}");

        foreach (var relativePath in new[] { "FTG_Game.csproj", "project.godot", "main.tscn", "global.json" })
        {
            var path = Path.Combine(templateDir, relativePath);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Required template file not found: {path}");
        }

        var sourceDirs = LoadFrameworkSourceDirs(repoRoot, hooks);
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
            Path.Combine("Scripts", "Framework", "Data", "example_physics_response_profiles.json"),
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

    private static string[] LoadFrameworkSourceDirs(string repoRoot, ScaffoldHooks hooks)
    {
        var manifestPath = Path.Combine(repoRoot, "Scaffold", "framework-source-dirs.txt");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Framework source manifest not found: {manifestPath}");

        ValidateExistingPathChain(Path.GetFullPath(repoRoot), manifestPath);
        hooks.BeforeFileAccess?.Invoke(manifestPath);
        ValidateExistingPathChain(Path.GetFullPath(repoRoot), manifestPath);
        var dirs = File.ReadAllLines(manifestPath)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToArray();
        if (dirs.Length == 0)
            throw new InvalidOperationException($"Framework source manifest is empty: {manifestPath}");
        foreach (var dir in dirs)
        {
            if (dir.Contains('\\') || Path.IsPathRooted(dir) || dir.Split('/').Any(part => part is "" or "." or "..") ||
                dir.Contains(':') || dir.StartsWith("//", StringComparison.Ordinal))
                throw new InvalidOperationException($"Invalid framework source manifest entry: {dir}");
            EnsureContained(Path.GetFullPath(repoRoot), Path.GetFullPath(dir, repoRoot), "manifest source");
        }
        if (dirs.Distinct(PathComparer).Count() != dirs.Length)
            throw new InvalidOperationException("Framework source manifest contains duplicate entries.");
        return dirs;
    }

    private static void CopyPlannedFile(ScaffoldPlan plan, PlannedFile entry, string projectName,
        ScaffoldArtifactTracker artifacts, ScaffoldHooks hooks)
    {
        hooks.BeforeFileAccess?.Invoke(entry.Source);
        hooks.BeforeFileAccess?.Invoke(entry.Destination);
        ValidateAccess(plan, entry.Source, entry.Destination);
        artifacts.EnsureDirectory(Path.GetDirectoryName(entry.Destination)!);
        ValidateAccess(plan, entry.Source, entry.Destination);
        using var source = new FileStream(entry.Source, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var destination = artifacts.CreateFile(entry.Destination);
        if (!entry.ReplacePlaceholders)
        {
            source.CopyTo(destination);
            return;
        }
        using var reader = new StreamReader(source);
        using var writer = new StreamWriter(destination);
        writer.Write(reader.ReadToEnd().Replace("{{FTG_PROJECT_NAME}}", projectName, StringComparison.Ordinal));
    }

    private static void ValidateAccess(ScaffoldPlan plan, string source, string destination)
    {
        ValidateExistingPathChain(plan.RepoRoot, source);
        ValidateTargetAccess(plan.TargetRoot, destination);
    }

    private static void EnsureContained(string root, string path, string label)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        path = Path.GetFullPath(path);
        if (!path.Equals(root, PathComparison) &&
            !path.StartsWith(root + Path.DirectorySeparatorChar, PathComparison))
            throw new InvalidOperationException($"Scaffold {label} escapes approved root: {path}");
    }

    private static void ValidateExistingPathChain(string root, string path)
    {
        EnsureContained(root, path, "source");
        var current = Path.GetFullPath(root);
        RejectReparsePoint(current);
        var relative = Path.GetRelativePath(current, Path.GetFullPath(path));
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
                throw new FileNotFoundException($"Required scaffold input not found: {current}");
            RejectReparsePoint(current);
        }
    }

    private static void ValidateTargetAccess(string targetRoot, string path)
    {
        EnsureContained(targetRoot, path, "destination");
        var current = Path.GetFullPath(path);
        while (!File.Exists(current) && !Directory.Exists(current))
            current = Directory.GetParent(current)?.FullName ?? throw new InvalidOperationException("Cannot resolve target parent.");
        for (var candidate = current; candidate is not null; candidate = Directory.GetParent(candidate)?.FullName)
        {
            RejectReparsePoint(candidate);
            if (string.Equals(candidate, Path.GetPathRoot(candidate), StringComparison.OrdinalIgnoreCase))
                break;
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException($"Scaffold paths must not traverse links or reparse points: {path}");
    }

    private static void ValidateTargetTree(string targetRoot)
    {
        ValidateTargetAccess(targetRoot, targetRoot);
        if (!Directory.Exists(targetRoot))
            return;
        foreach (var path in Directory.EnumerateFileSystemEntries(targetRoot, "*", SearchOption.AllDirectories))
            RejectReparsePoint(path);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

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
