#!/bin/bash
# Builds a self-contained Simitone.app for macOS.
# Usage: build-app.sh [arm64|x64] [output dir]   (defaults: this Mac's architecture, ./dist)
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
ARCH="${1:-$(uname -m)}"
[ "$ARCH" = "x86_64" ] && ARCH=x64
OUT="${2:-$HERE/dist}"
RID="osx-$ARCH"
APP="$OUT/Simitone.app"
PUBLISH="$OUT/publish-$RID"
VERSION="$(git -C "$HERE" describe --tags --always 2>/dev/null || echo dev)"

echo "Publishing $RID..."
rm -rf "$PUBLISH"
# NU1605 / duplicate outputs come from FreeSO's server libraries, which the client references but doesn't use.
dotnet publish "$HERE/Simitone.Mac.csproj" -c Release -r "$RID" --self-contained true \
    -p:NoWarn=NU1605 -p:ErrorOnDuplicatePublishOutputFiles=false -o "$PUBLISH"

echo "Assembling $APP..."
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
# the game runs from Resources: inside a bundle MonoGame looks for Content/ there, and FreeSO expects it next to
# the executable. Contents/MacOS only holds the launcher so code signing sees a normal bundle layout.
cp -R "$PUBLISH/." "$APP/Contents/Resources/"

cat > "$APP/Contents/MacOS/Simitone" <<'EOF'
#!/bin/bash
DIR="$(cd "$(dirname "$0")/../Resources" && pwd)"
exec "$DIR/Simitone" "$@"
EOF
chmod +x "$APP/Contents/MacOS/Simitone"

ICONSET="$OUT/Simitone.iconset"
rm -rf "$ICONSET" && mkdir -p "$ICONSET"
for size in 16 32 128 256; do
    sips -s format png -z $size $size "$HERE/../Simitone.Windows/Icon.bmp" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
    double=$((size * 2))
    [ $double -le 256 ] && sips -s format png -z $double $double "$HERE/../Simitone.Windows/Icon.bmp" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/Simitone.icns"
rm -rf "$ICONSET"

cat > "$APP/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>Simitone</string>
    <key>CFBundleDisplayName</key><string>Simitone</string>
    <key>CFBundleIdentifier</key><string>com.strixmcs-labs.simitone</string>
    <key>CFBundleExecutable</key><string>Simitone</string>
    <key>CFBundleIconFile</key><string>Simitone</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>$VERSION</string>
    <key>CFBundleVersion</key><string>$VERSION</string>
    <key>LSApplicationCategoryType</key><string>public.app-category.simulation-games</string>
    <key>LSMinimumSystemVersion</key><string>12.0</string>
    <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
EOF

# ad-hoc signature so Apple Silicon will run it locally (not notarised)
codesign --force --sign - "$APP"
rm -rf "$PUBLISH"
echo "Built $APP"
