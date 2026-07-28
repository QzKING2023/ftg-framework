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

    public static int Main(string[] args)
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
                return HandleNew(args.AsSpan(1));
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

    private static int HandleNew(ReadOnlySpan<string> args)
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

        targetDir = Path.GetFullPath(targetDir);

        // Check if target exists
        var targetExisted = Directory.Exists(targetDir);
        if (targetExisted)
        {
            var entries = Directory.GetFileSystemEntries(targetDir);
            if (entries.Length > 0)
            {
                Console.Error.WriteLine($"[Scaffold] Directory already exists and is not empty: {targetDir}");
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

        try
        {
            ProjectScaffolder.Scaffold(repoRoot, targetDir, projectName);
            Console.WriteLine();
            Console.WriteLine($"Project '{projectName}' created successfully!");
            Console.WriteLine();
            Console.WriteLine("Next steps:");
            Console.WriteLine($"  cd {targetDir}");
            Console.WriteLine("  dotnet restore");
            Console.WriteLine("  dotnet build");
            Console.WriteLine("  Open project.godot in the Godot editor");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Scaffold] Failed to create project: {ex.Message}");
            CleanupPartialScaffold(targetDir, targetExisted);
            return 1;
        }
    }

    // The scaffold only runs against a non-existent or empty target, so on failure
    // deleting everything under it restores the pre-run state and unblocks retries.
    private static void CleanupPartialScaffold(string targetDir, bool targetExisted)
    {
        try
        {
            if (!Directory.Exists(targetDir)) return;
            if (targetExisted)
            {
                foreach (var entry in Directory.GetFileSystemEntries(targetDir))
                {
                    if (Directory.Exists(entry))
                        Directory.Delete(entry, recursive: true);
                    else
                        File.Delete(entry);
                }
            }
            else
            {
                Directory.Delete(targetDir, recursive: true);
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

        // The name becomes the C# RootNamespace/AssemblyName and is substituted into
        // XML (csproj) and INI (project.godot), so it must be a valid C# identifier.
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
                CreateNoWindow = true
            });
            if (process is null)
            {
                PrintDotnetMissing();
                return false;
            }
            if (!process.WaitForExit(10000))
            {
                try { process.Kill(); } catch { /* best effort */ }
                Console.Error.WriteLine("[Scaffold] 'dotnet --version' timed out. Check your .NET SDK installation.");
                return false;
            }
            if (process.ExitCode != 0)
            {
                PrintDotnetMissing();
                return false;
            }
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // dotnet executable not found on PATH
            PrintDotnetMissing();
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Scaffold] Failed to probe dotnet: {ex.Message}");
            return false;
        }
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
        writer.WriteLine();
        writer.WriteLine("Examples:");
        writer.WriteLine("  ftg new MyFighter");
        writer.WriteLine("  ftg new MyFighter --output ./projects");
    }
}
