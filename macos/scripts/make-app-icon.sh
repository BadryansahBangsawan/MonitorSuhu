#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SRC="$ROOT/icon/MonitorSuhu-icon.png"
ICNS="$ROOT/macos/Resources/AppIcon.icns"
ICO="$ROOT/windows/src/MonitorSuhu.App/Assets/MonitorSuhu.ico"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

if [[ ! -f "$SRC" ]]; then
  echo "missing $SRC" >&2
  exit 1
fi

echo "==> Cropping icon to opaque bounds"
ICON_SRC="$SRC" ICON_DST="$WORK/icon-1024.png" swift -e '
import AppKit
let srcPath = ProcessInfo.processInfo.environment["ICON_SRC"]!
let destPath = ProcessInfo.processInfo.environment["ICON_DST"]!
let src = URL(fileURLWithPath: srcPath)
let dest = URL(fileURLWithPath: destPath)
guard let img = NSImage(contentsOf: src),
      let tiff = img.tiffRepresentation,
      let rep = NSBitmapImageRep(data: tiff) else {
    fputs("failed to load icon\n", stderr)
    exit(1)
}
var minX = rep.pixelsWide, minY = rep.pixelsHigh, maxX = 0, maxY = 0
for y in 0..<rep.pixelsHigh {
    for x in 0..<rep.pixelsWide {
        guard let c = rep.colorAt(x: x, y: y), c.alphaComponent > 0.04 else { continue }
        minX = min(minX, x); minY = min(minY, y)
        maxX = max(maxX, x); maxY = max(maxY, y)
    }
}
let pad = Int(Double(max(maxX - minX + 1, maxY - minY + 1)) * 0.10)
minX = max(0, minX - pad)
minY = max(0, minY - pad)
maxX = min(rep.pixelsWide - 1, maxX + pad)
maxY = min(rep.pixelsHigh - 1, maxY + pad)
let side = max(maxX - minX + 1, maxY - minY + 1)
let cx = (minX + maxX) / 2
let cy = (minY + maxY) / 2
var originX = cx - side / 2
var originY = cy - side / 2
originX = max(0, min(originX, rep.pixelsWide - side))
originY = max(0, min(originY, rep.pixelsHigh - side))
let rect = CGRect(x: originX, y: originY, width: side, height: side)
guard let cropped = rep.cgImage?.cropping(to: rect) else {
    fputs("crop failed\n", stderr)
    exit(1)
}
let scaled = NSImage(size: NSSize(width: 1024, height: 1024))
scaled.lockFocus()
NSGraphicsContext.current?.imageInterpolation = .high
NSImage(cgImage: cropped, size: NSSize(width: side, height: side))
    .draw(in: NSRect(x: 0, y: 0, width: 1024, height: 1024),
          from: .zero,
          operation: .copy,
          fraction: 1)
scaled.unlockFocus()
guard let tiffOut = scaled.tiffRepresentation,
      let pngRep = NSBitmapImageRep(data: tiffOut),
      let png = pngRep.representation(using: .png, properties: [:]) else {
    fputs("png encode failed\n", stderr)
    exit(1)
}
try! png.write(to: dest)
print("wrote \(dest.path) from \(side)x\(side)")
'

ICONSET="$WORK/AppIcon.iconset"
mkdir -p "$ICONSET"
declare -a SIZES=(16 32 48 64 128 256 512 1024)
for px in "${SIZES[@]}"; do
  sips -z "$px" "$px" "$WORK/icon-1024.png" --out "$WORK/icon-${px}.png" >/dev/null
done

cp "$WORK/icon-16.png"   "$ICONSET/icon_16x16.png"
cp "$WORK/icon-32.png"   "$ICONSET/icon_16x16@2x.png"
cp "$WORK/icon-32.png"   "$ICONSET/icon_32x32.png"
cp "$WORK/icon-64.png"   "$ICONSET/icon_32x32@2x.png"
cp "$WORK/icon-128.png"  "$ICONSET/icon_128x128.png"
cp "$WORK/icon-256.png"  "$ICONSET/icon_128x128@2x.png"
cp "$WORK/icon-256.png"  "$ICONSET/icon_256x256.png"
cp "$WORK/icon-512.png"  "$ICONSET/icon_256x256@2x.png"
cp "$WORK/icon-512.png"  "$ICONSET/icon_512x512.png"
cp "$WORK/icon-1024.png" "$ICONSET/icon_512x512@2x.png"

mkdir -p "$(dirname "$ICNS")"
iconutil -c icns "$ICONSET" -o "$ICNS"
echo "wrote $ICNS"

echo "==> Writing Windows ICO"
mkdir -p "$(dirname "$ICO")"
python3 - "$ICO" "$WORK/icon-16.png" "$WORK/icon-32.png" "$WORK/icon-48.png" "$WORK/icon-256.png" <<'PY'
import struct, sys
from pathlib import Path

dest = Path(sys.argv[1])
pngs = [Path(p).read_bytes() for p in sys.argv[2:]]

def ihdr(data: bytes):
    w, h = struct.unpack(">II", data[16:24])
    return w, h

count = len(pngs)
header = struct.pack("<HHH", 0, 1, count)
offset = 6 + 16 * count
entries = bytearray()
payload = bytearray()
for data in pngs:
    w, h = ihdr(data)
    entries += struct.pack(
        "<BBBBHHII",
        0 if w >= 256 else w,
        0 if h >= 256 else h,
        0,
        0,
        1,
        32,
        len(data),
        offset,
    )
    payload += data
    offset += len(data)
dest.write_bytes(header + entries + payload)
print(f"wrote {dest} ({count} pngs, {dest.stat().st_size} bytes)")
PY
