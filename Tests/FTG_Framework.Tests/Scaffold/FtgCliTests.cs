#nullable enable
using System.Diagnostics;
using System.IO.Compression;
using FTG_Framework.Scaffold;
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

    [Theory]
    [InlineData("", false, 0)]
    [InlineData("8.0.419", false, 8)]
    [InlineData("9.0.203", false, 9)]
    [InlineData("10.0.100", true, 10)]
    [InlineData("11.0.0-preview.1", true, 11)]
    public void SdkVersionPolicy_RequiresMajorTenOrLater(string version, bool expected, int expectedMajor)
    {
        Assert.Equal(expected, Program.IsSupportedSdkVersion(version, out var major));
        Assert.Equal(expectedMajor, major);
    }

    [Fact]
    public void GlobalJsonFiles_DeclareReviewedSdkPolicy()
    {
        var repoRoot = FindRepoRoot();
        foreach (var path in new[]
                 {
                     Path.Combine(repoRoot, "global.json"),
                     Path.Combine(repoRoot, "Scaffold", "ftg-project-template", "global.json")
                 })
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            var sdk = document.RootElement.GetProperty("sdk");
            Assert.Equal("10.0.0", sdk.GetProperty("version").GetString());
            Assert.Equal("latestMajor", sdk.GetProperty("rollForward").GetString());
            Assert.True(sdk.GetProperty("allowPrerelease").GetBoolean());
        }
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
            "Scripts/Framework/Engine/Physics",
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
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Data", "example_characters.json")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Data", "example_gatling.json")));

            // Engine — FrameData
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Engine", "FrameData", "FrameDataEngine.cs")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Engine", "FrameData", "CancelWindowTracker.cs")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Engine", "FrameData", "MoveTimeline.cs")));

            // Engine — Combo
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Engine", "Combo", "ComboExecutor.cs")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Engine", "Combo", "ChainValidator.cs")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "Engine", "Combo", "ComboStateTracker.cs")));

            // UI — Training panels
            Assert.True(File.Exists(Path.Combine(scriptsDir, "UI", "Training", "FrameDataPanel.cs")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "UI", "Training", "AdvantageDisplay.cs")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "UI", "Training", "InputLog.cs")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "UI", "Training", "PlaybackControls.cs")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "UI", "Training", "EventBusDebugPanel.cs")));
            Assert.True(File.Exists(Path.Combine(scriptsDir, "UI", "Training", "CharacterSelect.cs")));

            // UI — ViewModels
            Assert.True(Directory.Exists(Path.Combine(scriptsDir, "UI", "Training", "ViewModels")));

            // FrameRateManager
            Assert.True(File.Exists(Path.Combine(tmpDir, "FullModules", "Scripts", "FrameRateManager.cs")));
        }
        finally
        {
            TryDelete(tmpDir);
        }
    }

    [Fact]
    public void ScaffoldedProject_MatchesManifestAndRequiredResourceInventory()
    {
        var repoRoot = FindRepoRoot();
        var tmpDir = Path.Combine(Path.GetTempPath(), $"ftg_inventory_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            Assert.Equal(0, ProgramHarness.Run("new", "InventoryProject", "--output", tmpDir));
            var projectDir = Path.Combine(tmpDir, "InventoryProject");

            foreach (var sourceDir in ReadSourceManifest(repoRoot))
                AssertDirectoryParity(Path.Combine(repoRoot, sourceDir), Path.Combine(projectDir, sourceDir));

            var requiredFiles = new[]
            {
                "Scripts/FrameRateManager.cs",
                "Characters/character_template.tscn",
                "Scripts/Framework/Data/example_moves.json",
                "Scripts/Framework/Data/example_characters.json",
                "Scripts/Framework/Data/example_gatling.json",
                "Scripts/Framework/Data/example_knockback_profiles.json",
                "Scripts/Framework/Data/example_physics_response_profiles.json",
                "Scripts/Framework/Data/PhysicsProfileHotReloadService.cs",
                "Scripts/Framework/Data/PhysicsProfileReferenceValidator.cs",
                "Scripts/Framework/Data/template_fighter.json",
            };
            foreach (var relativePath in requiredFiles)
                Assert.True(File.Exists(Path.Combine(projectDir, relativePath)), $"Missing scaffold input: {relativePath}");
        }
        finally
        {
            TryDelete(tmpDir);
        }
    }

    [Fact]
    public void ScaffoldedProject_HasNoUnresolvedPlaceholders_AndResourcePathsCloseExactly()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"ftg_closure_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            Assert.Equal(0, ProgramHarness.Run("new", "ClosureProject", "--output", tmpDir));
            var projectDir = Path.Combine(tmpDir, "ClosureProject");
            var textExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".cs", ".csproj", ".godot", ".json", ".md", ".tscn"
            };

            foreach (var path in Directory.EnumerateFiles(projectDir, "*", SearchOption.AllDirectories)
                         .Where(path => textExtensions.Contains(Path.GetExtension(path))))
            {
                var content = File.ReadAllText(path);
                Assert.DoesNotContain("{{FTG_PROJECT_NAME}}", content);

                foreach (System.Text.RegularExpressions.Match match in
                         System.Text.RegularExpressions.Regex.Matches(content, "res://([^\"\\s\\)]+)"))
                {
                    var relativePath = match.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar);
                    AssertPathExistsWithExactCase(projectDir, relativePath);
                }
            }
        }
        finally
        {
            TryDelete(tmpDir);
        }
    }

    [Fact]
    public void Scaffold_MissingCharacterTemplate_FailsBeforeDestinationMutation()
    {
        var fixture = CreateMinimalScaffoldRepo(includeCharacterTemplate: false);
        var target = Path.Combine(Path.GetTempPath(), $"ftg_preflight_{Guid.NewGuid():N}");
        try
        {
            var ex = Assert.Throws<FileNotFoundException>(
                () => ProjectScaffolder.Scaffold(fixture, target, "PreflightProject"));
            Assert.Contains("character_template.tscn", ex.Message);
            Assert.False(Directory.Exists(target));
        }
        finally
        {
            TryDelete(fixture);
            TryDelete(target);
        }
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("Scripts/Framework/Core/../Data")]
    [InlineData("Scripts\\Framework\\Core")]
    [InlineData("/tmp/escape")]
    [InlineData("C:/escape")]
    [InlineData("//server/share")]
    public void Scaffold_InvalidManifestEntry_IsRejectedBeforeDestinationMutation(string entry)
    {
        var fixture = CreateMinimalScaffoldRepo(includeCharacterTemplate: true);
        var target = Path.Combine(Path.GetTempPath(), $"ftg_manifest_boundary_{Guid.NewGuid():N}");
        try
        {
            File.AppendAllText(Path.Combine(fixture, "Scaffold", "framework-source-dirs.txt"), entry + "\n");

            var ex = Assert.Throws<InvalidOperationException>(
                () => ProjectScaffolder.Scaffold(fixture, target, "BoundaryProject"));

            Assert.Contains("manifest entry", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(Directory.Exists(target));
        }
        finally
        {
            TryDelete(fixture);
            TryDelete(target);
        }
    }

    [Fact]
    public void Scaffold_DuplicateManifestAlias_IsRejectedBeforeDestinationMutation()
    {
        var fixture = CreateMinimalScaffoldRepo(includeCharacterTemplate: true);
        var target = Path.Combine(Path.GetTempPath(), $"ftg_manifest_duplicate_{Guid.NewGuid():N}");
        try
        {
            File.AppendAllText(Path.Combine(fixture, "Scaffold", "framework-source-dirs.txt"),
                "scripts/framework/core\n");

            Assert.Throws<InvalidOperationException>(
                () => ProjectScaffolder.Scaffold(fixture, target, "BoundaryProject"));
            Assert.False(Directory.Exists(target));
        }
        finally
        {
            TryDelete(fixture);
            TryDelete(target);
        }
    }

    [Theory]
    [InlineData("../Escape")]
    [InlineData("Bad/Name")]
    [InlineData("Bad\\Name")]
    [InlineData("Bad.Name")]
    [InlineData("CON")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("class")]
    public void Scaffold_DirectCallRejectsMaliciousProjectNameBeforeMutation(string projectName)
    {
        var fixture = CreateMinimalScaffoldRepo(includeCharacterTemplate: true);
        var target = Path.Combine(Path.GetTempPath(), $"ftg_project_name_{Guid.NewGuid():N}");
        try
        {
            Assert.Throws<ArgumentException>(() => ProjectScaffolder.Scaffold(fixture, target, projectName));
            Assert.False(Directory.Exists(target));
        }
        finally
        {
            TryDelete(fixture);
            TryDelete(target);
        }
    }

    [Fact]
    public void Scaffold_InvalidLateManifestEntry_NeverAccessesExternalSourceOrMutatesExistingTarget()
    {
        var fixture = CreateMinimalScaffoldRepo(includeCharacterTemplate: true);
        var target = Path.Combine(Path.GetTempPath(), $"ftg_existing_boundary_{Guid.NewGuid():N}");
        Directory.CreateDirectory(target);
        var sentinel = Path.Combine(target, "user-owned.bin");
        File.WriteAllBytes(sentinel, [1, 2, 3, 4]);
        var accesses = new List<string>();
        try
        {
            File.AppendAllText(Path.Combine(fixture, "Scaffold", "framework-source-dirs.txt"),
                "Scripts/Framework/Core\n../../external\n");
            var hooks = new ScaffoldHooks(BeforeFileAccess: accesses.Add);

            Assert.Throws<InvalidOperationException>(() => ProjectScaffolder.Scaffold(
                fixture, target, "BoundaryProject", new ScaffoldArtifactTracker(), hooks));

            Assert.Equal([1, 2, 3, 4], File.ReadAllBytes(sentinel));
            Assert.Single(Directory.GetFileSystemEntries(target));
            Assert.All(accesses, path => Assert.True(
                Path.GetFullPath(path).StartsWith(Path.GetFullPath(fixture), StringComparison.OrdinalIgnoreCase),
                $"Unexpected external access: {path}"));
        }
        finally
        {
            TryDelete(fixture);
            TryDelete(target);
        }
    }

    [Fact]
    public void Scaffold_SourceDirectoryLinkEscape_IsRejectedBeforeMutation()
    {
        var fixture = CreateMinimalScaffoldRepo(includeCharacterTemplate: true);
        var external = Path.Combine(Path.GetTempPath(), $"ftg_external_source_{Guid.NewGuid():N}");
        var target = Path.Combine(Path.GetTempPath(), $"ftg_link_source_target_{Guid.NewGuid():N}");
        Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(external, "secret.txt"), "unchanged");
        var core = Path.Combine(fixture, "Scripts", "Framework", "Core");
        Directory.Delete(core, recursive: true);
        try
        {
            try { Directory.CreateSymbolicLink(core, external); }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                return;
            }

            Assert.Throws<InvalidOperationException>(
                () => ProjectScaffolder.Scaffold(fixture, target, "BoundaryProject"));
            Assert.Equal("unchanged", File.ReadAllText(Path.Combine(external, "secret.txt")));
            Assert.False(Directory.Exists(target));
        }
        finally
        {
            TryDelete(fixture);
            TryDelete(target);
            TryDelete(external);
        }
    }

    [Fact]
    public void Scaffold_DestinationDirectoryLinkEscape_IsRejectedBeforeMutation()
    {
        var fixture = CreateMinimalScaffoldRepo(includeCharacterTemplate: true);
        var external = Path.Combine(Path.GetTempPath(), $"ftg_external_target_{Guid.NewGuid():N}");
        var target = Path.Combine(Path.GetTempPath(), $"ftg_link_destination_{Guid.NewGuid():N}");
        Directory.CreateDirectory(external);
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(external, "sentinel.txt"), "unchanged");
        var scriptsLink = Path.Combine(target, "Scripts");
        try
        {
            try { Directory.CreateSymbolicLink(scriptsLink, external); }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                return;
            }

            Assert.Throws<InvalidOperationException>(
                () => ProjectScaffolder.Scaffold(fixture, target, "BoundaryProject"));
            Assert.Equal("unchanged", File.ReadAllText(Path.Combine(external, "sentinel.txt")));
            Assert.Single(Directory.GetFileSystemEntries(target));
        }
        finally
        {
            TryDelete(fixture);
            TryDelete(target);
            TryDelete(external);
        }
    }

    [Fact]
    public void New_RestoreFailure_ReturnsNonZeroAndCleansNewDestination()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"ftg_restore_failure_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            var hooks = new ScaffoldHooks(
                Restore: _ => throw new InvalidOperationException("injected restore failure"));
            var exitCode = ProgramHarness.RunWithHooks(hooks, "new", "RestoreFailure", "--output", tmpDir);
            Assert.NotEqual(0, exitCode);
            var target = Path.Combine(tmpDir, "RestoreFailure");
            Assert.False(Directory.Exists(target),
                Directory.Exists(target) ? string.Join(", ", Directory.GetFileSystemEntries(target)) : "");
        }
        finally
        {
            TryDelete(tmpDir);
        }
    }

    [Fact]
    public void New_RestoreFailure_PreservesPreExistingEmptyDestination()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"ftg_restore_existing_{Guid.NewGuid():N}");
        var target = Path.Combine(tmpDir, "ExistingEmpty");
        Directory.CreateDirectory(target);
        try
        {
            var hooks = new ScaffoldHooks(
                Restore: _ => throw new TimeoutException("injected restore timeout"));
            var exitCode = ProgramHarness.RunWithHooks(hooks, "new", "ExistingEmpty", "--output", tmpDir);
            Assert.NotEqual(0, exitCode);
            Assert.True(Directory.Exists(target));
            Assert.Empty(Directory.GetFileSystemEntries(target));
        }
        finally
        {
            TryDelete(tmpDir);
        }
    }

    [Fact]
    public void New_FailurePreservesContentAddedToPreExistingDestinationDuringScaffold()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"ftg_restore_concurrent_{Guid.NewGuid():N}");
        var target = Path.Combine(tmpDir, "ExistingEmpty");
        Directory.CreateDirectory(target);
        var userFile = Path.Combine(target, "user-created.txt");
        try
        {
            var hooks = new ScaffoldHooks(Restore: _ =>
            {
                File.WriteAllText(userFile, "preserve me");
                throw new InvalidOperationException("injected restore failure");
            });

            Assert.NotEqual(0, ProgramHarness.RunWithHooks(
                hooks, "new", "ExistingEmpty", "--output", tmpDir));
            Assert.Equal("preserve me", File.ReadAllText(userFile));
            Assert.Single(Directory.GetFileSystemEntries(target));
        }
        finally
        {
            TryDelete(tmpDir);
        }
    }

    [Theory]
    [InlineData("template")]
    [InlineData("manifest")]
    [InlineData("manifest-directory")]
    [InlineData("frame-rate-manager")]
    public void Scaffold_MissingRequiredInput_FailsBeforeDestinationMutation(string missing)
    {
        var fixture = CreateMinimalScaffoldRepo(includeCharacterTemplate: true);
        var target = Path.Combine(Path.GetTempPath(), $"ftg_missing_{Guid.NewGuid():N}");
        try
        {
            var path = missing switch
            {
                "template" => Path.Combine(fixture, "Scaffold", "ftg-project-template", "main.tscn"),
                "manifest" => Path.Combine(fixture, "Scaffold", "framework-source-dirs.txt"),
                "manifest-directory" => Path.Combine(fixture, "Scripts", "Framework", "Core"),
                "frame-rate-manager" => Path.Combine(fixture, "Scripts", "FrameRateManager.cs"),
                _ => throw new ArgumentOutOfRangeException(nameof(missing))
            };
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            else
                File.Delete(path);

            Assert.ThrowsAny<Exception>(() => ProjectScaffolder.Scaffold(fixture, target, "MissingInput"));
            Assert.False(Directory.Exists(target));
        }
        finally
        {
            TryDelete(fixture);
            TryDelete(target);
        }
    }

    [Fact]
    public void New_PlaceholderFailure_ReturnsNonZeroAndCleansDestination()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"ftg_placeholder_failure_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            var hooks = new ScaffoldHooks(
                BeforePlaceholderReplacement: _ => throw new IOException("injected placeholder failure"));
            Assert.NotEqual(0, ProgramHarness.RunWithHooks(
                hooks, "new", "PlaceholderFailure", "--output", tmpDir));
            Assert.False(Directory.Exists(Path.Combine(tmpDir, "PlaceholderFailure")));
        }
        finally
        {
            TryDelete(tmpDir);
        }
    }

    [Theory]
    [InlineData("start")]
    [InlineData("timeout")]
    [InlineData("non-zero")]
    public void New_AllRestoreFailureModesAreFatalAndAtomic(string mode)
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"ftg_restore_mode_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        var hooks = new ScaffoldHooks(Restore: _ => throw mode switch
        {
            "start" => new System.ComponentModel.Win32Exception("injected start failure"),
            "timeout" => new TimeoutException("injected timeout"),
            "non-zero" => new InvalidOperationException("injected exit 42"),
            _ => new ArgumentOutOfRangeException(nameof(mode))
        });
        try
        {
            Assert.NotEqual(0, ProgramHarness.RunWithHooks(
                hooks, "new", "RestoreMode", "--output", tmpDir));
            Assert.False(Directory.Exists(Path.Combine(tmpDir, "RestoreMode")));
        }
        finally
        {
            TryDelete(tmpDir);
        }
    }

    [Fact]
    public async Task ScaffoldedProject_BuildsSuccessfully()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"ftg_test_build_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            var projectName = "BuildTest";
            var exitCode = ProgramHarness.Run("new", projectName, "--output", tmpDir);
            Assert.Equal(0, exitCode);

            var projectDir = Path.Combine(tmpDir, projectName);

            using var build = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{projectName}.csproj\" --no-restore",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            Assert.NotNull(build);
            var stdoutTask = build.StandardOutput.ReadToEndAsync();
            var stderrTask = build.StandardError.ReadToEndAsync();
            await WaitForExitOrKillAsync(build, 120_000);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (build.ExitCode != 0)
                Assert.Fail($"dotnet build failed with exit code {build.ExitCode}.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        }
        finally
        {
            TryDelete(tmpDir);
        }
    }

    [Fact]
    public async Task ScaffoldedProject_ResolvesEffectiveSdkFromGeneratedDirectory()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"ftg_sdk_resolution_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            Assert.Equal(0, ProgramHarness.Run("new", "SdkResolution", "--output", tmpDir));
            var result = await RunProcess("dotnet", "--version", Path.Combine(tmpDir, "SdkResolution"), 30_000);
            Assert.Equal(0, result.ExitCode);
            Assert.True(Program.IsSupportedSdkVersion(result.Stdout.Trim(), out var major),
                $"Generated directory resolved unsupported SDK: {result.Stdout}\n{result.Stderr}");
            Assert.True(major >= 10);
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
    public void OnboardingDocs_DeclareToolchainFirstRunAndDistributionBoundary()
    {
        var repoRoot = FindRepoRoot();
        var templateGuide = File.ReadAllText(Path.Combine(repoRoot, "docs", "project-template.md"));
        var addonReadme = File.ReadAllText(Path.Combine(repoRoot, "addons", "ftg-framework", "README.md"));

        foreach (var document in new[] { templateGuide, addonReadme })
        {
            Assert.Contains(".NET SDK 10+", document, StringComparison.Ordinal);
            Assert.Contains("net8.0", document, StringComparison.Ordinal);
            Assert.Contains("MyFighter", document, StringComparison.Ordinal);
            Assert.Contains("A, D, S, and Space", document, StringComparison.Ordinal);
            Assert.Contains("U", document, StringComparison.Ordinal);
            Assert.Contains("under five minutes", document, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("repository checkout", document, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("standalone", document, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Asset Library submission", document, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains("Godot Asset Library (planned)", addonReadme, StringComparison.Ordinal);
        Assert.DoesNotContain("Search for \"FTG Framework\"", addonReadme, StringComparison.Ordinal);
        Assert.Contains("Program.IsSupportedSdkVersion", templateGuide, StringComparison.Ordinal);
        Assert.Contains("repository-root `global.json`", templateGuide, StringComparison.Ordinal);
    }

    [Fact]
    public void GodotSmokeHarness_IsDocumentedAndWaitsForP1ReadyState()
    {
        var verificationDir = Path.Combine(FindRepoRoot(), "Scaffold", "verification");
        var guide = File.ReadAllText(Path.Combine(verificationDir, "README.md"));
        var harness = File.ReadAllText(Path.Combine(verificationDir, "ScaffoldSmokeTest.cs"));

        Assert.Contains("Copy-Item", guide, StringComparison.Ordinal);
        Assert.Contains("res://scaffold_smoke.tscn", guide, StringComparison.Ordinal);
        Assert.Contains("InitializationTimeoutFrames", harness, StringComparison.Ordinal);
        Assert.Contains("character.PlayerId == 1", harness, StringComparison.Ordinal);
        Assert.Contains("visible P1 InputLog missing", harness, StringComparison.Ordinal);
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
            Assert.Contains(names, n => n.EndsWith("src/Data/example_moves.json"));
            Assert.Contains(names, n => n.EndsWith("src/Data/example_knockback_profiles.json"));
            Assert.Contains(names, n => n.EndsWith("src/Editor/FTGEditorPlugin.cs"));
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

    private static string[] ReadSourceManifest(string repoRoot)
    {
        return File.ReadAllLines(Path.Combine(repoRoot, "Scaffold", "framework-source-dirs.txt"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Replace('/', Path.DirectorySeparatorChar))
            .ToArray();
    }

    private static void AssertDirectoryParity(string expectedDir, string actualDir)
    {
        Assert.True(Directory.Exists(actualDir), $"Missing copied directory: {actualDir}");
        var expected = Directory.EnumerateFiles(expectedDir, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".uid", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Split(Path.DirectorySeparatorChar)
                .Any(part => part is "bin" or "obj" or ".godot"))
            .Select(path => Path.GetRelativePath(expectedDir, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var actual = Directory.EnumerateFiles(actualDir, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(actualDir, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, actual);
        foreach (var relativePath in expected)
            Assert.Equal(File.ReadAllBytes(Path.Combine(expectedDir, relativePath)),
                File.ReadAllBytes(Path.Combine(actualDir, relativePath)));
    }

    private static void AssertPathExistsWithExactCase(string root, string relativePath)
    {
        var current = root;
        foreach (var segment in relativePath.Split(Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var exact = Directory.EnumerateFileSystemEntries(current)
                .FirstOrDefault(entry => string.Equals(Path.GetFileName(entry), segment, StringComparison.Ordinal));
            Assert.True(exact is not null, $"Resource path does not resolve with exact case: {relativePath}");
            current = exact!;
        }
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcess(
        string fileName, string arguments, string workingDirectory, int timeoutMs)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        Assert.NotNull(process);
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await WaitForExitOrKillAsync(process, timeoutMs);
        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static async Task WaitForExitOrKillAsync(Process process, int timeoutMs)
    {
        using var timeout = new CancellationTokenSource(timeoutMs);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            catch (InvalidOperationException)
            {
                // The process exited between cancellation and termination.
            }
            throw new TimeoutException($"Process '{process.StartInfo.FileName}' timed out after {timeoutMs} ms.");
        }
    }

    private static string CreateMinimalScaffoldRepo(bool includeCharacterTemplate)
    {
        var root = Path.Combine(Path.GetTempPath(), $"ftg_fixture_{Guid.NewGuid():N}");
        var template = Path.Combine(root, "Scaffold", "ftg-project-template");
        Directory.CreateDirectory(template);
        File.WriteAllText(Path.Combine(template, "FTG_Game.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework><RootNamespace>{{FTG_PROJECT_NAME}}</RootNamespace></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(template, "project.godot"),
            "config/name=\"{{FTG_PROJECT_NAME}}\"\nrun/main_scene=\"res://main.tscn\"");
        File.WriteAllText(Path.Combine(template, "main.tscn"), "[gd_scene format=3]\n[node name=\"Main\" type=\"Node\"]");
        File.WriteAllText(Path.Combine(template, "global.json"),
            "{\"sdk\":{\"version\":\"10.0.0\",\"rollForward\":\"latestMajor\",\"allowPrerelease\":true}}");

        var manifest = Path.Combine(root, "Scaffold", "framework-source-dirs.txt");
        File.WriteAllText(manifest, "Scripts/Framework/Core\nScripts/Framework/Data\n");
        Directory.CreateDirectory(Path.Combine(root, "Scripts", "Framework", "Core"));
        File.WriteAllText(Path.Combine(root, "Scripts", "Framework", "Core", "GameLoop.cs"), "namespace Fixture;");
        var dataDir = Path.Combine(root, "Scripts", "Framework", "Data");
        Directory.CreateDirectory(dataDir);
        foreach (var name in new[]
                 {
                     "example_moves.json", "example_characters.json", "example_gatling.json",
                     "example_knockback_profiles.json", "example_physics_response_profiles.json",
                     "template_fighter.json"
                 })
            File.WriteAllText(Path.Combine(dataDir, name), "[]");

        Directory.CreateDirectory(Path.Combine(root, "Scripts"));
        File.WriteAllText(Path.Combine(root, "Scripts", "FrameRateManager.cs"), "namespace Fixture;");
        Directory.CreateDirectory(Path.Combine(root, "Characters"));
        if (includeCharacterTemplate)
            File.WriteAllText(Path.Combine(root, "Characters", "character_template.tscn"), "[gd_scene format=3]");
        return root;
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
