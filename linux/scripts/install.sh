#!/usr/bin/env bash
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
DEST="${MONITORSUHU_HOME:-$HOME/.local/opt/MonitorSuhu}"
BIN="${HOME}/.local/bin"
APPS="${HOME}/.local/share/applications"

mkdir -p "$DEST" "$BIN" "$APPS"
cp -a "$HERE"/. "$DEST/"
chmod +x "$DEST/MonitorSuhu"
ln -sf "$DEST/MonitorSuhu" "$BIN/MonitorSuhu"

cat > "$APPS/monitorsuhu.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=MonitorSuhu
Comment=Hardware temperature overlay
Exec=$DEST/MonitorSuhu
Terminal=false
Categories=Utility;System;
StartupNotify=false
EOF

echo "Installed to $DEST"
echo "Run:  $DEST/MonitorSuhu"
if echo ":$PATH:" | grep -q ":$BIN:"; then
  echo "Or:   MonitorSuhu"
else
  echo "Add $BIN to PATH to run: MonitorSuhu"
fi
