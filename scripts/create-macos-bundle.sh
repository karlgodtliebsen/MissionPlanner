#!/usr/bin/env bash
# Creates MissionPlanner.app from a self-contained macOS publish folder.
#
#   dotnet publish src/Platforms/MissionPlanner.MacOs -c Release -r osx-arm64 --self-contained -o out/osx-arm64
#   scripts/create-macos-bundle.sh out/osx-arm64 out 2.0.0
#
# Run on macOS: it needs sips, iconutil and codesign. The result is ad-hoc signed, not notarized, so the
# first start needs "Open" from the context menu, or: xattr -dr com.apple.quarantine MissionPlanner.app
set -euo pipefail

publish_dir="${1:?usage: $0 <publish-dir> <output-dir> [version]}"
output_dir="${2:?usage: $0 <publish-dir> <output-dir> [version]}"
version="${3:-2.0.0}"
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
plist_template="$repo_root/src/Platforms/MissionPlanner.MacOs/Info.plist"
icon_png="$repo_root/src/UI/MissionPlanner.Themes/Resources/AppIcon/mpdesktop.png"

app="$output_dir/MissionPlanner.app"
rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -R "$publish_dir"/. "$app/Contents/MacOS/"
chmod +x "$app/Contents/MacOS/MissionPlanner"
sed "s/@VERSION@/$version/g" "$plist_template" > "$app/Contents/Info.plist"

if [[ -f "$icon_png" ]]; then
  iconset="$(mktemp -d)/MissionPlanner.iconset"
  mkdir -p "$iconset"
  for size in 16 32 64 128 256 512; do
    sips -z "$size" "$size" "$icon_png" --out "$iconset/icon_${size}x${size}.png" > /dev/null
    sips -z "$((size * 2))" "$((size * 2))" "$icon_png" --out "$iconset/icon_${size}x${size}@2x.png" > /dev/null
  done
  iconutil -c icns "$iconset" -o "$app/Contents/Resources/MissionPlanner.icns"
fi

codesign --force --deep --sign - "$app"
echo "Created $app"
