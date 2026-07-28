#nullable enable
using System.Diagnostics;
using System.IO.Compression;
using Xunit;

namespace FTG_Framework.Tests.Scaffold;

public class FtgCliTests
{
    [Fact]
    public void New_WithNoArgs_ReturnsNonZero()
    {
        var exitCode = ProgramHarness.Run("new");
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void New_WithEmptyProjectName_ReturnsNonZero()
    {
        var exitCode = ProgramHarness.Run("new", "");
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void New_WithSpacesInName_ReturnsNonZero()
    {
        var exitCode = ProgramHarness.Run("new", "Bad Name");
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void New_WithPathTraversal_ReturnsNonZero()
    {
        var exitCode = ProgramHarness.Run("new", "../escape");
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void New_WithInvalidChars_ReturnsNonZero()
    {
        var exitCode = ProgramHarness.Run("new", "bad<name>");
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void New_WithExistingDirectory_ReturnsNonZero()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"ftg_test_existing_{Guid.NewGuid():N}");
        var projectDir = Path.Combine(tmpDir, "ExistingDir");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "dummy.txt"), "test");
        try
        {
            var exitCode = ProgramHarness.Run("new", "ExistingDir", "--output", tmpDir);
            Assert.NotEqual(0, exitCode);
        }
        finally
        {
            TryDelete(tmpDir);
        }
    }

    [Fact]
    public void New_WithUnknownFlag_ReturnsNonZero()
    {
        var exitCode = ProgramHarness.Run("new", "ValidName", "--outpt", "/tmp");
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void New_WithInvalidCSharpIdentifier_ReturnsNonZero()
    {
        Assert.NotEqual(0, ProgramHarness.Run("new", "my-fighter"));
        Assert.NotEqual(0, ProgramHarness.Run("new", "9Lives"));
    }

    [Fact]
    public void New_WithValidName_CreatesProject()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"ftg_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            var projectName = "ValidProject";
            var exitCode = ProgramHarness.Run("new", projectName, "--output", tmpDir);
            Assert.Equal(0, exitCode);

            var projectDir = Path.Combine(tmpDir, projectName);
            Assert.True(Directory.Exists(projectDir));
            Assert.True(File.Exists(Path.Combine(projectDir, "project.godot")));
            Assert.True(File.Exists(Path.Combine(projectDir, $"{projectName}.csproj")));
            Assert.True(File.Exists(Path.Combine(projectDir, "main.tscn")));
            Assert.True(Directory.Exists(Path.Combine(projectDir, "Scripts", "Framework", "Core")));
            Assert.True(File.Exists(Path.Combine(projectDir, "Scripts", "Framework", "Core", "EventBus.cs")));
            Assert.True(File.Exists(Path.Combine(projectDir, "Scripts", "FrameRateManager.cs")));
        }
        finally
        {
            TryDelete(tmpDir);
        }
    }

    [Fact]
    public void New_WithEmptyDirectory_Overwrites()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"ftg_test_{Guid.NewGuid():N}");
        var projectName = "EmptyDir";
        var projectDir = Path.Combine(tmpDir, projectName);
        Directory.CreateDirectory(projectDir); // Empty directory
        try
        {
            var exitCode = ProgramHarness.Run("new", projectName, "--output", tmpDir);
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(Path.Combine(projectDir, "project.godot")));
        }
        finally
        {
            TryDelete(tmpDir);
        }
    }

    [Fact]
    public void PlaceholderReplacement_ProducesCorrectConfig()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"ftg_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            var projectName = "MyGame";
            ProgramHarness.Run("new", projectName, "--output", tmpDir);

            var godotConfig = File.ReadAllText(Path.Combine(tmpDir, projectName, "project.godot"));
            Assert.Contains($"config/name=\"{projectName}\"", godotConfig);
            Assert.Contains($"project/assembly_name=\"{projectName}\"", godotConfig);
            Assert.DoesNotContain("{{FTG_PROJECT_NAME}}", godotConfig);

            var csprojContent = File.ReadAllText(Path.Combine(tmpDir, projectName, $"{projectName}.csproj"));
            Assert.Contains($"<RootNamespace>{projectName}</RootNamespace>", csprojContent);
            Assert.Contains($"<AssemblyName>{projectName}</AssemblyName>", csprojContent);
            Assert.DoesNotContain("{{FTG_PROJECT_NAME}}", csprojContent);
        }
        finally
        {
            TryDelete(tmpDir);
        }
    }

    [Fact]
    public void TemplateFiles_AreWellFormed()
    {
        var repoRoot = FindRepoRoot();
        Assert.NotNull(repoRoot);

        var templateDir = Path.Combine(repoRoot, "Scaffold", "ftg-project-template");

        // project.godot exists and has required sections
        var godotPath = Path.Combine(templateDir, "project.godot");
        Assert.True(File.Exists(godotPath));
        var godotContent = File.ReadAllText(godotPath);
        Assert.Contains("[application]", godotContent);
        Assert.Contains("[autoload]", godotContent);
        Assert.Contains("[dotnet]", godotContent);
        Assert.Contains("GameLoop", godotContent);
        Assert.Contains("FrameRateManager", godotContent);

        // .csproj exists and is valid XML
        var csprojPath = Path.Combine(templateDir, "FTG_Game.csproj");
        Assert.True(File.Exists(csprojPath));
        var csprojContent = File.ReadAllText(csprojPath);
        Assert.Contains("Godot.NET.Sdk", csprojContent);
        Assert.Contains("net8.0", csprojContent);

        // scene file exists
        Assert.True(File.Exists(Path.Combine(templateDir, "main.tscn")));

        // .gitignore exists
        var gitignorePath = Path.Combine(templateDir, ".gitignore");
        Assert.True(File.Exists(gitignorePath));
        var gitignoreContent = File.ReadAllText(gitignorePath);
        Assert.Contains(".godot/", gitignoreContent);
        Assert.Contains("bin/", gitignoreContent);
        Assert.Contains("obj/", gitignoreContent);
    }

    [Fact]
    public void AllFrameworkSourceDirectories_Exist()
    {
        var repoRoot = FindRepoRoot();
        Assert.NotNull(repoRoot);

        var sourceDirs = new[]
        {
            "Scripts/Framework/Core",
            "Scripts/Framework/Core/Events",
            "Scripts/Framework/Input",
            "Scripts/Framework/Data",
            "Scripts/Framework/Engine/FrameData",
            "Scripts/Framework/Engine/Combo",
            "Scripts/Framework/UI/Training",
            "Scripts/Framework/UI/Training/ViewModels",
        };

        foreach (var dir in sourceDirs)
        {
            var fullPath = Path.Combine(repoRoot, dir);
            Assert.True(Directory.Exists(fullPath), $"Directory should exist: {dir}");
            var csFiles = Directory.GetFiles(fullPath, "*.cs", SearchOption.TopDirectoryOnly);
            Assert.NotEmpty(csFiles);
        }
    }

    [Fact]
    public void ScaffoldedProject_ContainsCompleteModuleSet()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"ftg_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            ProgramHarness.Run("new", "FullModules", "--output", tmpDir);
            var scriptsDir = Path.Combine(tmpDir, "FullModules", "Scripts", "Framework");

            // Core
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Core", "EventBus.cs")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Core", "Pool.cs")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Core", "EventBusDebugService.cs")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Core", "GameLoop.cs")));

            // Events directory
            Assert.True(Directory.Exists(Path.Combine(scriptsDir, "Core", "Events")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Core", "Events", "HitConnectedEvent.cs")));

            // Input
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Input", "InputHistory.cs")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Input", "DefaultSOCDResolver.cs")));

            // Data
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Data", "DataStore.cs")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Data", "example_moves.json")));

            // Engine
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Engine", "FrameData", "FrameDataEngine.cs")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Engine", "Combo", "ComboExecutor.cs")));

            // UI
            Assert.True(File.Exists(Path.Combine(scriptsDir, "UI", "Training", "FrameDataPanel.cs")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "UI", "Training", "EventBusDebugPanel.cs")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "UI", "Training", "CharacterSelect.cs")));

            // FrameRateManager
            Assert.True(File.Exists(Path.Combine(tmpDir, "FullModules", "Scripts", "FrameRateManager.cs")));
        }
        finally
        {
            TryDelete(tmpDir);
        }
    }

    [Fact]
    public void NoArgs_PrintsUsageAndExitsNonZero()
    {
        var exitCode = ProgramHarness.Run();
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void PackageAddon_ProducesValidZip()
    {
        var repoRoot = FindRepoRoot();
        var addonSrc = Path.Combine(repoRoot, "addons", "ftg-framework", "src");

        var (fileName, arguments) = OperatingSystem.IsWindows()
            ? ("powershell", "-NoProfile -ExecutionPolicy Bypass -File Scaffold/package-addon.ps1")
            : ("bash", "Scaffold/package-addon.sh");

        Process? process;
        try
        {
            try
            {
                process = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = repoRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return; // Script runtime unavailable on this machine — nothing to assert against.
            }
            Assert.NotNull(process);
            Assert.True(process.WaitForExit(120000), "Packaging script timed out");
            Assert.Equal(0, process.ExitCode);

            // Version comes from plugin.cfg — locate the produced zip by glob.
            var zips = Directory.GetFiles(Path.Combine(repoRoot, "Scaffold"), "ftg-framework-*.zip");
            Assert.NotEmpty(zips);

            using var zip = ZipFile.OpenRead(zips[0]);
            var names = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();

            Assert.Contains(names, n => n == "ftg-framework/plugin.cfg");
            Assert.Contains(names, n => n == "ftg-framework/README.md");
            Assert.Contains(names, n => n.EndsWith("src/Core/GameLoop.cs.template"));
            Assert.DoesNotContain(names, n => n.EndsWith("GameLoop.template.cs"));
            Assert.DoesNotContain(names, n => n.EndsWith(".uid", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(names, n => n.Contains("src/UI/Training/ViewModels/"));
            Assert.Contains(names, n => n.EndsWith("src/FrameRateManager.cs"));
        }
        finally
        {
            // Clean up generated files so they don't confuse Godot's C# type scanner.
            TryDelete(addonSrc);
            foreach (var zip in Directory.GetFiles(Path.Combine(repoRoot, "Scaffold"), "ftg-framework-*.zip"))
                TryDeleteFile(zip);
        }
    }

    private static string FindRepoRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (true)
        {
            if (Directory.Exists(Path.Combine(current, "Scripts", "Framework", "Core")) &&
                Directory.Exists(Path.Combine(current, "Scaffold", "ftg-project-template")))
            {
                return current;
            }
            var parent = Directory.GetParent(current);
            if (parent is null)
                throw new InvalidOperationException("Cannot find repo root. Run tests from the FTG Framework repo directory.");
            current = parent.FullName;
        }
    }

    private static void TryDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
            // A still-finishing restore process can hold locks; don't mask the test result.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
