#!/usr/bin/env python3
"""Draw Area11.ico - the picture the owner sees on the desktop (F-47, D-19).

Why a script and not a checked-in drawing: the icon must be the **brand colour of the current
version** (the same hex as `BuildInfo.BrandHex`), and the owner's rule is that every release changes
the colour. Regenerating it from the same source keeps the icon and the sign-in screen identical:

    python3 tools/make_icon.py

Output: `dotnet/Area11.Desktop/Resources/area11.ico` - a classic 32-bit BMP icon with four sizes
(16, 32, 48, 64), which every Windows version since XP reads. PNG-compressed icons are smaller but
need Vista; the compiler is happiest with this form.

The drawing is a rounded copper square with a white capsule across it - a tablet, for a pharmacy.
Every pixel is supersampled 3x3, so the edges are smooth at 64 px and still clear at 16 px.
"""

from __future__ import annotations

import re
import pathlib
import struct

ROOT = pathlib.Path(__file__).resolve().parents[1]
BUILD_INFO = ROOT / "dotnet" / "Area11.Core" / "Utils" / "BuildInfo.cs"
OUT = ROOT / "dotnet" / "Area11.Desktop" / "Resources" / "area11.ico"

SIZES = (16, 32, 48, 64)
AA = 3  # samples per axis


def brand_rgb() -> tuple[int, int, int]:
    """The brand hex of this version, read from BuildInfo.cs - one source of truth."""
    text = BUILD_INFO.read_text(encoding="utf-8")
    match = re.search(r'BrandHex\s*=\s*"#([0-9A-Fa-f]{6})"', text)
    if match is None:
        raise SystemExit("BuildInfo.cs has no BrandHex")
    value = match.group(1)
    return int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16)


def inside_rounded_square(x: float, y: float, radius: float) -> bool:
    """True for a point in the unit square whose corners are rounded by `radius`."""
    if x < 0 or y < 0 or x > 1 or y > 1:
        return False
    # Distance to the nearest corner circle, measured from the corner's centre.
    cx = radius if x < radius else (1 - radius if x > 1 - radius else x)
    cy = radius if y < radius else (1 - radius if y > 1 - radius else y)
    return (x - cx) ** 2 + (y - cy) ** 2 <= radius ** 2


def distance_to_segment(px: float, py: float, ax: float, ay: float, bx: float, by: float) -> float:
    dx, dy = bx - ax, by - ay
    length = dx * dx + dy * dy
    t = 0.0 if length == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / length))
    cx, cy = ax + t * dx, ay + t * dy
    return ((px - cx) ** 2 + (py - cy) ** 2) ** 0.5


def render(size: int, brand: tuple[int, int, int]) -> list[list[tuple[int, int, int, int]]]:
    """One size as rows of RGBA, top row first."""
    radius = 0.20
    pill_radius = 0.150
    a = (0.315, 0.685)
    b = (0.685, 0.315)

    rows: list[list[tuple[int, int, int, int]]] = []
    for py in range(size):
        row: list[tuple[int, int, int, int]] = []
        for px in range(size):
            red = green = blue = alpha = 0.0
            for sy in range(AA):
                for sx in range(AA):
                    x = (px + (sx + 0.5) / AA) / size
                    y = (py + (sy + 0.5) / AA) / size

                    if not inside_rounded_square(x, y, radius):
                        continue

                    if distance_to_segment(x, y, a[0], a[1], b[0], b[1]) <= pill_radius:
                        red += 255.0
                        green += 255.0
                        blue += 255.0
                    else:
                        red += brand[0]
                        green += brand[1]
                        blue += brand[2]

                    alpha += 1.0

            samples = AA * AA
            if alpha == 0:
                row.append((0, 0, 0, 0))
                continue

            row.append((
                round(red / alpha),
                round(green / alpha),
                round(blue / alpha),
                round(255.0 * alpha / samples),
            ))
        rows.append(row)

    return rows


def bmp_image(rows: list[list[tuple[int, int, int, int]]]) -> bytes:
    """A BITMAPINFOHEADER + bottom-up BGRA pixels + an all-opaque AND mask."""
    size = len(rows)
    pixels = bytearray()
    for y in range(size - 1, -1, -1):
        for red, green, blue, alpha in rows[y]:
            pixels += bytes((blue, green, red, alpha))

    mask_stride = ((size + 31) // 32) * 4
    mask = bytes(mask_stride * size)

    header = struct.pack("<IiiHHIIiiII", 40, size, size * 2, 1, 32, 0, 0, 0, 0, 0, 0)
    return header + bytes(pixels) + mask


def write_icon() -> int:
    brand = brand_rgb()
    images = [bmp_image(render(size, brand)) for size in SIZES]

    body = b""
    offset = 6 + 16 * len(SIZES)
    entries = b""
    for size, image in zip(SIZES, images):
        entries += struct.pack(
            "<BBBBHHII",
            size if size < 256 else 0,
            size if size < 256 else 0,
            0, 0, 1, 32, len(image), offset,
        )
        body += image
        offset += len(image)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_bytes(struct.pack("<HHH", 0, 1, len(SIZES)) + entries + body)
    return len(SIZES)


if __name__ == "__main__":
    count = write_icon()
    print(f"wrote {OUT.relative_to(ROOT)} with {count} sizes, brand {brand_rgb()}")
