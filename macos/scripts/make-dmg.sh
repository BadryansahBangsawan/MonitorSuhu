#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' Resources/Info.plist)"
APP="build/MonitorSuhu.app"
STAGING="build/dmg-staging"
OUT_DIR="$ROOT/dist"
DMG="$OUT_DIR/MonitorSuhu-${VERSION}-macos.dmg"

echo "==> Building MonitorSuhu.app"
make -C "$ROOT" app

if command -v codesign >/dev/null; then
  echo "==> Ad-hoc signing"
  codesign --force --sign - --identifier id.monitorsuhu.app "$APP/Contents/MacOS/MonitorSuhu"
  codesign --force --sign - --identifier id.monitorsuhu.app "$APP"
fi

echo "==> Staging DMG"
# Old UpdateInstaller enumerates -mountroot and does not descend mount
# points. The volume name must be MonitorSuhu.app and the volume root
# must be the bundle (Contents/ at root) so that single enumerator hit
# is a valid app. Nested MonitorSuhu/MonitorSuhu.app fails on 1.0.8.
rm -rf "$STAGING"
mkdir -p "$STAGING"
ditto "$APP/" "$STAGING/"

mkdir -p "$OUT_DIR"
rm -f "$DMG"

echo "==> Creating $DMG"
hdiutil create \
  -volname "MonitorSuhu.app" \
  -srcfolder "$STAGING" \
  -ov \
  -format UDZO \
  "$DMG"

rm -rf "$STAGING"
echo "Created $DMG"
ls -lh "$DMG"
