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
rm -rf "$STAGING"
mkdir -p "$STAGING"
ditto "$APP" "$STAGING/MonitorSuhu.app"
ln -s /Applications "$STAGING/Applications"

mkdir -p "$OUT_DIR"
rm -f "$DMG"

echo "==> Creating $DMG"
hdiutil create \
  -volname "MonitorSuhu" \
  -srcfolder "$STAGING" \
  -ov \
  -format UDZO \
  "$DMG"

rm -rf "$STAGING"
echo "Created $DMG"
ls -lh "$DMG"
