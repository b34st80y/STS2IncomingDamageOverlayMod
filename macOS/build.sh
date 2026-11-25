#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
pack="${1:-$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/Resources/Slay the Spire 2.pck}"
app="${OVERLAY_OUTPUT_DIR:-artifacts/macOS}/STS2 Damage Overlay.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
python3 macOS/prepare-assets.py "$pack" "$app/Contents/Resources"
cp macOS/Info.plist "$app/Contents/Info.plist"
xcrun swiftc -O -target "$(uname -m)-apple-macos14.0" \
    -module-cache-path "${TMPDIR:-/tmp}/sts2-overlay-module-cache" \
    macOS/*.swift -o "$app/Contents/MacOS/STS2DamageOverlay"
codesign --force --sign - "$app"
echo "Built $app"
