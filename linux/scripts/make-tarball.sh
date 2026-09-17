#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

VERSION="$(tr -d '[:space:]' < VERSION)"
PUBLISH="$ROOT/linux/dist/linux-x64"
OUT="$ROOT/linux/dist/MonitorSuhu-${VERSION}-linux-x64.tar.gz"

if [[ ! -x "$PUBLISH/MonitorSuhu" && ! -f "$PUBLISH/MonitorSuhu" ]]; then
  echo "missing $PUBLISH/MonitorSuhu — publish first" >&2
  exit 1
fi

cp "$ROOT/linux/scripts/install.sh" "$PUBLISH/install.sh"
chmod +x "$PUBLISH/install.sh" "$PUBLISH/MonitorSuhu"

mkdir -p "$ROOT/linux/dist"
rm -f "$OUT"
tar -C "$PUBLISH" -czf "$OUT" .
echo "Created $OUT"
ls -lh "$OUT"
