#!/usr/bin/env bash
# FTG Framework — Addon packaging script (Unix/macOS)
# Usage: ./Scaffold/package-addon.sh [version]
# Output: Scaffold/ftg-framework-<version>.zip (default version read from plugin.cfg)
# Zip layout: top-level ftg-framework/ folder — extract into the project's addons/ directory.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
ADDON_DIR="$REPO_ROOT/addons/ftg-framework"
PLUGIN_CFG="$ADDON_DIR/plugin.cfg"
SRC_DIR="$ADDON_DIR/src"
OUTPUT_DIR="$REPO_ROOT/Scaffold"
MANIFEST="$SCRIPT_DIR/framework-source-dirs.txt"

if [ "${1:-}" != "" ]; then
    VERSION="$1"
else
    VERSION="$(sed -n 's/^version="\([^"]*\)".*/\1/p' "$PLUGIN_CFG" | tr -d '\r' | head -1)"
    [ -n "$VERSION" ] || { echo "ERROR: could not read version from $PLUGIN_CFG" >&2; exit 1; }
fi

ZIP_PATH="$OUTPUT_DIR/ftg-framework-$VERSION.zip"

command -v zip >/dev/null 2>&1 || { echo "ERROR: 'zip' not found on PATH" >&2; exit 1; }
[ -f "$MANIFEST" ] || { echo "ERROR: manifest not found: $MANIFEST" >&2; exit 1; }

echo "Packaging FTG Framework $VERSION..."

# Clean and recreate src directory
rm -rf "$SRC_DIR"
mkdir -p "$SRC_DIR"

copy_framework_dir() {
    local src="$1" dst="$2"
    [ -d "$src" ] || { echo "ERROR: framework source directory missing: $src" >&2; exit 1; }
    mkdir -p "$dst"
    # Process substitution keeps the loop in the main shell so set -e catches cp failures.
    while read -r f; do
        rel="${f#./}"
        # Top-level GameLoop.cs ships separately as GameLoop.cs.template
        [ "$rel" = "GameLoop.cs" ] && continue
        mkdir -p "$dst/$(dirname "$rel")"
        cp "$src/$rel" "$dst/$rel"
        echo "  COPY $rel"
    done < <(cd "$src" && find . -type f \
        ! -iname '*.uid' \
        ! -path './bin/*' ! -path './obj/*' ! -path './.godot/*' \
        ! -path '*/bin/*' ! -path '*/obj/*' ! -path '*/.godot/*' \
        -print)
}

# Shared module manifest — single source of truth for CLI and packagers.
# Pre-validate the manifest has content before entering the pipeline so an
# empty/commented-out manifest is caught as an error, not a cryptic pipefail.
MANIFEST_DIRS="$(grep -v '^\s*#' "$MANIFEST" | grep -v '^\s*$' || true)"
if [ -z "$MANIFEST_DIRS" ]; then
    echo "ERROR: manifest is empty or has no entries: $MANIFEST" >&2
    exit 1
fi
echo "$MANIFEST_DIRS" | while read -r dir; do
    # Addon layout strips the Scripts/Framework/ prefix
    rel="${dir#Scripts/Framework/}"
    copy_framework_dir "$REPO_ROOT/$dir" "$SRC_DIR/$rel"
done

# Copy FrameRateManager.cs
[ -f "$REPO_ROOT/Scripts/FrameRateManager.cs" ] || { echo "ERROR: FrameRateManager.cs not found" >&2; exit 1; }
cp "$REPO_ROOT/Scripts/FrameRateManager.cs" "$SRC_DIR/"
echo "  COPY FrameRateManager.cs"

# Copy GameLoop as a template with a non-.cs extension so host projects don't compile it
[ -f "$REPO_ROOT/Scripts/Framework/Core/GameLoop.cs" ] || { echo "ERROR: GameLoop.cs not found" >&2; exit 1; }
cp "$REPO_ROOT/Scripts/Framework/Core/GameLoop.cs" "$SRC_DIR/Core/GameLoop.cs.template"
echo "  COPY Core/GameLoop.cs.template"

# Bundle the repo LICENSE if one exists (required for Asset Library submission)
if [ -f "$REPO_ROOT/LICENSE" ]; then
    cp "$REPO_ROOT/LICENSE" "$ADDON_DIR/"
    echo "  COPY LICENSE"
else
    echo "  WARN no LICENSE at repo root — Asset Library submission requires one"
fi

# Create zip with a top-level ftg-framework/ folder. Rewrite the repository/CLI
# script path only for the package-local src/Editor layout, then restore it.
PLUGIN_CFG_BACKUP="$PLUGIN_CFG.ftg-package-backup"
cp "$PLUGIN_CFG" "$PLUGIN_CFG_BACKUP"
trap 'mv -f "$PLUGIN_CFG_BACKUP" "$PLUGIN_CFG"' EXIT
sed -i.bak 's#^script="[^"]*"#script="src/Editor/FTGEditorPlugin.cs"#' "$PLUGIN_CFG"
rm -f "$PLUGIN_CFG.bak" "$ZIP_PATH"
(cd "$ADDON_DIR/.." && zip -r "$ZIP_PATH" "ftg-framework/") > /dev/null
mv -f "$PLUGIN_CFG_BACKUP" "$PLUGIN_CFG"
trap - EXIT

echo ""
echo "Addon packaged: $ZIP_PATH"
echo "Done."
