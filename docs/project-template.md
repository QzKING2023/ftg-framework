# FTG Framework — Project Template

The project template enables developers to scaffold a working Godot project with FTG Framework integrated in a single command.

## Template Directory Structure

```
Scaffold/ftg-project-template/
├── project.godot              # Application config + autoloads
├── FTG_Game.csproj            # .csproj (renamed by CLI to {name}.csproj)
├── main.tscn                  # Minimal main scene
├── global.json                # .NET SDK version pinning (SDK 10, matches repo root)
├── icon.svg                   # Default icon (Godot generates icon.svg.import on first open)
├── .gitignore                 # Standard Godot C# ignore rules
└── Scripts/                   # Empty — framework source copied here by CLI
```

## Module Manifest

`Scaffold/framework-source-dirs.txt` is the single source of truth for which
framework directories ship to users. It is read by:

- the CLI (`ProjectScaffolder.LoadFrameworkSourceDirs`)
- `Scaffold/package-addon.ps1`
- `Scaffold/package-addon.sh`

All files inside a listed directory are copied recursively (excluding `.uid`,
`bin/`, `obj/`, `.godot/`), so new files in an existing module need no manifest
change; a new module directory gets one new line in the manifest.
`Tests/FTG_Framework.Tests/Scaffold/FtgCliTests.cs` asserts the scaffolded
module set as a regression guard.

## Extension Points

### Adding a New Framework Module

When a new module is added to the framework (e.g., Physics engine):

1. **Add the source directory to the manifest:**
   Edit `Scaffold/framework-source-dirs.txt`, e.g., add `Scripts/Framework/Engine/Physics`.

2. **Verify the module registers in GameLoop:**
   The template copies `GameLoop.cs` verbatim from `Scripts/Framework/Core/GameLoop.cs`.
   If the new module requires GameLoop initialization, update `GameLoop.cs` in the framework repo.

3. **Add data files (if any):**
   If the module includes JSON config files, they will be copied automatically
   when the parent directory (`Data/`) is listed in the manifest.

4. **Update the module-set test:**
   Extend `ScaffoldedProject_ContainsCompleteModuleSet` in `FtgCliTests.cs`.

### Adding a New Autoload

1. Add the autoload entry to `Scaffold/ftg-project-template/project.godot` under `[autoload]`
2. If the autoload script is in a new directory, list the directory in `Scaffold/framework-source-dirs.txt`

### Updating SDK or .NET Version

1. Update `Scaffold/ftg-project-template/global.json` with the new SDK version
2. Update `Scaffold/ftg-project-template/FTG_Game.csproj` with any target framework changes
3. Update the minimum requirements in `addons/ftg-framework/README.md`
   and the CLI's missing-dotnet message in `Scaffold/ftg-cli/Program.cs`

## CLI Architecture

```
Scaffold/ftg-cli/
├── FtgCli.csproj              # Standalone .NET 8 console app
├── Program.cs                 # Entry point, argument parsing, validation
└── ProjectScaffolder.cs       # Template copy, placeholder replacement, source copy
```

The CLI:
- Finds the framework repo root by walking up from CWD or assembly location
- Copies template files, renames `.csproj`, replaces `{{FTG_PROJECT_NAME}}` placeholders
- Copies all files from the directories listed in `Scaffold/framework-source-dirs.txt`
- Runs `dotnet restore` on the scaffolded project (output visible, exit code checked)

### Running the CLI

The CLI runs from a framework repository checkout — it locates the template and
framework source on disk relative to the repo root:

```bash
dotnet run --project Scaffold/ftg-cli -- new MyFighter [--output <path>]
```

There is no standalone binary distribution: a published `ftg` executable outside
a repo checkout cannot find the template/framework payload and exits with an error.
(If standalone distribution is ever needed, the template and framework source must
be embedded into the binary first.)

## Addon Packaging

```
addons/ftg-framework/           # plugin.cfg + README committed; src/ generated
├── plugin.cfg                  # Godot plugin metadata (committed, version source of truth)
├── README.md                   # Setup instructions (committed)
└── src/                        # Framework source (generated, .gitignored)
    ├── Core/
    │   ├── GameLoop.cs.template  # Reference GameLoop (non-.cs extension → not compiled)
    │   └── ...
    ├── Input/
    └── ...
```

To build the addon package:
```powershell
# Windows
.\Scaffold\package-addon.ps1

# Unix
./Scaffold/package-addon.sh
```

The output is `Scaffold/ftg-framework-<version>.zip` with a top-level
`ftg-framework/` folder (extract into the project's `addons/` directory). The version
defaults to the `version` field in `addons/ftg-framework/plugin.cfg`; pass `-Version` / `$1` to override.

The packaging scripts:
- Copy all files from the manifest directories (excluding `.uid`, `bin/`, `obj/`, `.godot/`)
- Fail loudly if any manifest directory or required file is missing
- Copy `GameLoop.cs` as `GameLoop.cs.template`
- Copy `FrameRateManager.cs`
- Bundle the repo-root `LICENSE` when present (warn otherwise)

## Testing the Template

```bash
# Scaffold a test project
dotnet run --project Scaffold/ftg-cli -- new TestProject --output /tmp

# Build it
cd /tmp/TestProject
dotnet build

# Run framework tests (in framework repo, not scaffolded project)
cd /path/to/ftg-framework
dotnet test FTG_Framework.sln
```

## Distribution

1. **Godot Asset Library:** Upload `Scaffold/ftg-framework-<version>.zip` to the Asset Library.
   Submission requires a `LICENSE` file at the repo root (the packagers bundle it automatically).
2. **GitHub Releases:** Attach the addon zip.
3. **CLI:** Distributed as source — users run it from a repo checkout (see above).
