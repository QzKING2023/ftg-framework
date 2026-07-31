#nullable enable

namespace FTG_Framework.Scaffold;

public static class Program
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static readonly HashSet<string> CSharpKeywords = new()
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
        "checked", "class", "const", "continue", "decimal", "default", "delegate", "do",
        "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
        "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int",
        "interface", "internal", "is", "lock", "long", "namespace", "new", "null",
        "object", "operator", "out", "override", "params", "private", "protected",
        "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
        "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
        "var", "virtual", "void", "volatile", "while"
    };

    public static int Main(string[] args)
        => Run(args, new ScaffoldHooks());

    internal static int Run(string[] args, ScaffoldHooks hooks)
    {
        if (args.Length == 0)
        {
            PrintUsage(Console.Error);
            return 1;
        }

        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "new":
                return HandleNew(args.AsSpan(1), hooks);
            case "help":
            case "--help":
            case "-h":
                PrintUsage(Console.Out);
                return 0;
            default:
                Console.Error.WriteLine($"[Scaffold] Unknown command: {command}");
                PrintUsage(Console.Error);
                return 1;
        }
    }

    private static int HandleNew(ReadOnlySpan<string> args, ScaffoldHooks hooks)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("[Scaffold] Missing project name.");
            Console.Error.WriteLine("Usage: ftg new <project-name> [--output <path>]");
            return 1;
        }

        var projectName = args[0];

        string? outputPath = null;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].Equals("--output", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("[Scaffold] Missing value for --output.");
                    return 1;
                }
                outputPath = args[++i];
                if (string.IsNullOrEmpty(outputPath))
                {
                    Console.Error.WriteLine("[Scaffold] --output value must not be empty.");
                    return 1;
                }
            }
            else
            {
                Console.Error.WriteLine($"[Scaffold] Unknown argument: {args[i]}");
                return 1;
            }
        }

        // Validate project name
        var nameError = ValidateProjectName(projectName);
        if (nameError is not null)
        {
            Console.Error.WriteLine($"[Scaffold] Invalid project name: {nameError}");
            return 1;
        }

        // Determine target directory
        var targetDir = outputPath is not null
            ? Path.Combine(outputPath, projectName)
            : Path.Combine(Directory.GetCurrentDirectory(), projectName);

        try
        {
            targetDir = Path.GetFullPath(targetDir);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Scaffold] Invalid target path: {ex.Message}");
            return 1;
        }

        // Check if target exists
        var targetExisted = Directory.Exists(targetDir);
        if (targetExisted)
        {
            try
            {
                var entries = Directory.GetFileSystemEntries(targetDir);
                if (entries.Length > 0)
                {
                    Console.Error.WriteLine($"[Scaffold] Directory already exists and is not empty: {targetDir}");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Scaffold] Cannot read target directory: {ex.Message}");
                return 1;
            }
        }

        // Find repo root (where Scripts/Framework/ and Scaffold/ live)
        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            Console.Error.WriteLine("[Scaffold] Cannot find framework repository root. Run from the FTG Framework repo directory.");
            return 1;
        }

        // Check dotnet is available
        if (!CheckDotnetAvailable())
        {
            return 1;
        }

        // Scaffold
        Console.WriteLine($"Creating project '{projectName}' in {targetDir}...");
        var artifacts = new ScaffoldArtifactTracker();

        try
        {
            ProjectScaffolder.Scaffold(repoRoot, targetDir, projectName, artifacts, hooks);
            Console.WriteLine();
            Console.WriteLine($"Project '{projectName}' created successfully!");
            Console.WriteLine();
            Console.WriteLine("Next steps:");
            Console.WriteLine($"  cd {targetDir}");
            Console.WriteLine("  dotnet build");
            Console.WriteLine("  Open project.godot in the Godot editor");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Scaffold] Failed to create project: {ex.Message}");
            CleanupPartialScaffold(targetDir, artifacts);
            return 1;
        }
    }

    private static void CleanupPartialScaffold(string targetDir, ScaffoldArtifactTracker artifacts)
    {
        try
        {
            foreach (var file in artifacts.Files)
            {
                try
                {
                    if (File.Exists(file))
                        File.Delete(file);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Scaffold] Warning: could not remove '{file}': {ex.Message}");
                }
            }

            foreach (var directory in artifacts.Directories.OrderByDescending(path => path.Length))
            {
                try
                {
                    if (Directory.Exists(directory) &&
                        Directory.GetFileSystemEntries(directory).Length == 0)
                        Directory.Delete(directory);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Scaffold] Warning: could not remove '{directory}': {ex.Message}");
                }
            }
        }
        catch (Exception cleanupEx)
        {
            Console.Error.WriteLine($"[Scaffold] Warning: could not clean up partial project at {targetDir}: {cleanupEx.Message}");
        }
    }

    private static string? ValidateProjectName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Project name must not be empty.";

        if (name.Contains(' '))
            return "Project name must not contain spaces.";

        if (ReservedNames.Contains(name))
            return "Project name is a reserved operating system name.";

        if (name.All(c => c == '_'))
            return "Project name must not be only underscores.";

        if (CSharpKeywords.Contains(name))
            return "Project name must not be a C# reserved keyword (it becomes the namespace).";

        if (!char.IsLetter(name[0]) && name[0] != '_')
            return "Project name must start with a letter or underscore (it becomes the C# namespace).";

        if (name.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
            return "Project name may only contain letters, digits, and underscores (it becomes the C# namespace).";

        return null;
    }

    private static string? FindRepoRoot()
    {
        return FindRepoRootFrom(Directory.GetCurrentDirectory())
            ?? FindRepoRootFrom(AppContext.BaseDirectory);
    }

    private static string? FindRepoRootFrom(string startDir)
    {
        var current = startDir;
        while (true)
        {
            if (Directory.Exists(Path.Combine(current, "Scripts", "Framework", "Core")) &&
                Directory.Exists(Path.Combine(current, "Scaffold", "ftg-project-template")))
            {
                return current;
            }
            var parent = Directory.GetParent(current);
            if (parent is null) return null;
            current = parent.FullName;
        }
    }

    private static bool CheckDotnetAvailable()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            });
            if (process is null)
            {
                PrintDotnetMissing();
                return false;
            }

            if (!process.WaitForExit(10000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                Console.Error.WriteLine("[Scaffold] 'dotnet --version' timed out. Check your .NET SDK installation.");
                return false;
            }

            if (process.ExitCode != 0)
            {
                PrintDotnetMissing();
                return false;
            }

            var versionOutput = process.StandardOutput.ReadToEnd().Trim();
            if (!TryParseMajorVersion(versionOutput, out var major))
            {
                Console.Error.WriteLine("[Scaffold] Could not determine .NET SDK version. Install .NET SDK 10+ from https://dotnet.microsoft.com/download");
                return false;
            }

            if (!IsSupportedSdkVersion(versionOutput, out major))
            {
                Console.Error.WriteLine($"[Scaffold] .NET SDK {major}.x detected, but SDK 10+ is required. Install .NET SDK 10+ from https://dotnet.microsoft.com/download");
                return false;
            }

            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            PrintDotnetMissing();
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Scaffold] Failed to probe dotnet: {ex.Message}");
            return false;
        }
    }

    private static bool TryParseMajorVersion(string versionOutput, out int major)
    {
        major = 0;
        if (string.IsNullOrWhiteSpace(versionOutput)) return false;
        var trimmed = versionOutput.TrimStart('v', 'V');
        var dotIndex = trimmed.IndexOf('.');
        var numberPart = dotIndex > 0 ? trimmed[..dotIndex] : trimmed;
        return int.TryParse(numberPart, out major);
    }

    internal static bool IsSupportedSdkVersion(string versionOutput, out int major)
    {
        return TryParseMajorVersion(versionOutput, out major) && major >= 10;
    }

    private static void PrintDotnetMissing()
    {
        Console.Error.WriteLine("[Scaffold] .NET SDK not found. Install .NET SDK 10+ from https://dotnet.microsoft.com/download");
    }

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("FTG Framework — Project Scaffold Tool");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  ftg new <project-name> [--output <path>]");
        writer.WriteLine("  ftg --help");
        writer.WriteLine();
        writer.WriteLine("The CLI runs from an FTG Framework repository checkout:");
        writer.WriteLine("  dotnet run --project Scaffold/ftg-cli -- new <project-name>");
        writer.WriteLine("  Requires .NET SDK 10+; generated projects target net8.0.");
        writer.WriteLine("  Project names must be valid C# identifiers (for example, MyFighter).");
        writer.WriteLine();
        writer.WriteLine("Examples:");
        writer.WriteLine("  ftg new MyFighter");
        writer.WriteLine("  ftg new MyFighter --output ./projects");
        writer.WriteLine();
        writer.WriteLine("First run: open project.godot, press Play, use A/D/S/Space for directions and U for 5LP.");
        writer.WriteLine("Standalone CLI publishing and Asset Library submission remain future release work.");
    }
}
