# FTG Framework — Asset Inventory

## Overview

This project is a code library/framework with minimal Godot-specific assets. The framework itself contains no game assets (sprites, audio, models). Game content is provided by the consuming project.

## Asset Summary

| Category | Count | Notes |
|----------|-------|-------|
| Icons | 1 | `icon.svg` — default Godot project icon |
| Shader Cache | ~10 files | Godot internal, auto-generated (`.godot/shader_cache/`) |
| GDScript | 5 files | Rider plugin only (`addons/rider-plugin/`) |

## Key Assets

| Path | Type | Size | Description |
|------|------|------|-------------|
| `icon.svg` | Vector | small | Default Godot project icon |
| `project.godot` | Config | small | Godot project configuration |

## Note

This is a framework/library project. The consuming game project provides all gameplay assets (sprites, animations, audio, etc.). The framework itself ships with zero gameplay assets. The only binary outputs are compiled .NET assemblies.

## Future Assets (Consuming Projects)

When using FTG Framework in a game project, the following asset types are expected:

- **Sprites/Textures**: Character sprites, effects
- **Audio**: SFX, music
- **Animations**: Godot animation resources
- **Scenes**: Character scenes, UI scenes, training mode
- **JSON Data**: Move definitions, Gatling tables
