"""
Restyles every sprite into one look: a shared muted night palette (fits the navy fog), near-black
outlines, a little less saturated and "cartoony"; and generates the dark wooden floor tiles.

    python tools/art/restyle.py

Reads the originals from tools/art/source/ (copied there from factory-td/Assets/Sprites on the first
run) and writes factory-td/Assets/Sprites/, so it can be re-run with a tweaked palette without
degrading the art. Tiles/floor_wood.png is generated from scratch (4 variants side by side).
"""
import os
import random
import shutil
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SPRITES = os.path.join(ROOT, "factory-td", "Assets", "Sprites")
SOURCE = os.path.join(ROOT, "tools", "art", "source")

OUTLINE = (13, 10, 20)

PALETTE_HEX = [
    # navy / night blues
    "0d0a14", "161528", "1f2240", "2a3158", "3b4a7a", "5268a0", "7a95c8", "b4c8e6",
    # teals
    "1c3a44", "2a5a5e", "3e8a80", "6cc0a8",
    # greens
    "23402a", "3a6b35", "5f9a45", "9cc466",
    # wood / browns
    "2b1d1c", "3e2a24", "57392c", "7a5236", "a5733f", "c99a5c",
    # warm lamp light
    "8a3b1f", "c4602a", "e38b35", "f2b64a", "f7dd7a", "fff3c4",
    # reds / pinks
    "4a1a2a", "7e2536", "b8384a", "e0646a",
    # purples
    "3a2350", "5e3a7a", "8a5ea8",
    # greys
    "2e2e3a", "4a4a58", "6e6e80", "9a9aac", "c4c6d4",
]
PALETTE = [tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) for h in PALETTE_HEX]


def srgb_to_lab(c):
    def lin(v):
        v /= 255.0
        return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
    r, g, b = (lin(v) for v in c)
    x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047
    y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883

    def f(t):
        return t ** (1 / 3) if t > 0.008856 else 7.787 * t + 16 / 116
    return 116 * f(y) - 16, 500 * (f(x) - f(y)), 200 * (f(y) - f(z))


PALETTE_LAB = [srgb_to_lab(c) for c in PALETTE]
_cache = {}


def muted(c):
    """A little less saturated and a little darker: less cartoon, more night."""
    r, g, b = c
    grey = 0.299 * r + 0.587 * g + 0.114 * b
    k = 0.62  # keep 62% of the saturation
    r, g, b = (grey + (v - grey) * k for v in (r, g, b))
    return tuple(max(0, min(255, int(v * 0.86))) for v in (r, g, b))


def nearest(c):
    if c in _cache:
        return _cache[c]
    lab = srgb_to_lab(muted(c))
    best = min(range(len(PALETTE)), key=lambda i: sum((a - b) ** 2 for a, b in zip(lab, PALETTE_LAB[i])))
    _cache[c] = PALETTE[best]
    return PALETTE[best]


def luminance(c):
    return (0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]) / 255.0


def restyle(path_in, path_out, outline=True):
    img = Image.open(path_in).convert("RGBA")
    w, h = img.size
    src = img.load()
    out = Image.new("RGBA", (w, h))
    dst = out.load()

    def solid(x, y):
        return 0 <= x < w and 0 <= y < h and src[x, y][3] >= 200

    for y in range(h):
        for x in range(w):
            r, g, b, a = src[x, y]
            if a == 0:
                continue
            if a < 200:
                # soft shadows / halos: keep the alpha, tint towards the night
                dst[x, y] = (8, 8, 24, a) if luminance((r, g, b)) < 0.3 else (*nearest((r, g, b)), a)
                continue
            edge = outline and any(not solid(x + dx, y + dy) and 0 <= x + dx < w and 0 <= y + dy < h
                                   for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if edge or luminance((r, g, b)) < 0.2:
                dst[x, y] = (*OUTLINE, 255)  # clear black outlines (and the old dark ink lines)
            else:
                dst[x, y] = (*nearest((r, g, b)), 255)
    out.save(path_out)


def floor_tiles(path_out, variants=4, size=64):
    """Dark wooden planks: four rows of planks per tile, staggered seams, grain, the odd knot."""
    rng = random.Random(1234)
    woods = [(58, 40, 33), (66, 45, 36), (74, 50, 38), (62, 43, 36), (52, 37, 31)]
    seam = (24, 17, 20)
    img = Image.new("RGBA", (size * variants, size))
    px = img.load()
    for v in range(variants):
        row_h = size // 4
        for row in range(4):
            # where this row's planks end (staggered per row and variant)
            cuts = sorted({0, size} | {rng.randrange(12, size - 12) for _ in range(rng.choice((1, 1, 2)))})
            for a, b in zip(cuts, cuts[1:]):
                base = rng.choice(woods)
                shift = rng.randint(-4, 4)
                grain = [rng.random() for _ in range(row_h)]
                knot = (rng.randrange(a + 3, max(a + 4, b - 3)), row * row_h + rng.randrange(4, row_h - 4)) if rng.random() < 0.25 and b - a > 10 else None
                for y in range(row * row_h, (row + 1) * row_h):
                    ly = y - row * row_h
                    for x in range(a, b):
                        c = [ch + shift for ch in base]
                        # long grain streaks with small breaks
                        if grain[ly] > 0.72 and (x * 7 + ly * 3 + v) % 11 != 0:
                            c = [ch - 7 for ch in c]
                        elif grain[ly] < 0.12:
                            c = [ch + 5 for ch in c]
                        if ly == 1:
                            c = [ch + 9 for ch in c]  # soft top light of each plank
                        if knot and (x - knot[0]) ** 2 + ((y - knot[1]) * 2) ** 2 <= 5:
                            c = [ch - 16 for ch in c]
                        if ly == 0 or x == a:
                            c = list(seam)  # gaps between planks
                        px[v * size + x, y] = (*[max(0, min(255, int(ch))) for ch in c], 255)
    img.save(path_out)


def wall_tile(path_out, size=64):
    """Dark slate stone blocks in staggered rows, fitting the night palette."""
    rng = random.Random(99)
    stones = [(38, 44, 70), (44, 52, 82), (34, 40, 64), (48, 57, 88)]
    mortar = OUTLINE
    img = Image.new("RGBA", (size, size))
    px = img.load()
    row_h = 16
    for row in range(size // row_h):
        offset = 0 if row % 2 == 0 else 16
        for block in range(-1, size // 32 + 1):
            x0 = block * 32 + offset
            base = rng.choice(stones)
            for y in range(row * row_h, (row + 1) * row_h):
                for x in range(max(0, x0), min(size, x0 + 32)):
                    ly, lx = y - row * row_h, x - x0
                    c = list(base)
                    if ly in (1, 2) and 1 < lx < 30:
                        c = [ch + 14 for ch in c]  # lit top edge
                    elif ly >= row_h - 3:
                        c = [ch - 10 for ch in c]  # shaded bottom
                    if (x * 13 + y * 7) % 23 == 0:
                        c = [ch - 8 for ch in c]  # speckles
                    if ly == 0 or lx == 0:
                        c = list(mortar)
                    px[x, y] = (*[max(0, min(255, ch)) for ch in c], 255)
    img.save(path_out)


def main():
    if not os.path.isdir(SOURCE):
        shutil.copytree(SPRITES, SOURCE, ignore=shutil.ignore_patterns("*.import"))
    for folder, _, files in os.walk(SOURCE):
        rel = os.path.relpath(folder, SOURCE)
        for name in files:
            if not name.endswith(".png"):
                continue
            src = os.path.join(folder, name)
            dst = os.path.join(SPRITES, rel, name)
            if rel == "Tiles" and name in ("floor_wood.png", "wall.png"):
                continue
            # Full-tile pictures (belts, walls, deposits on the floor) get no outline at their edges.
            restyle(src, dst, outline=rel not in ("Tiles", "Conveyors"))
    floor_tiles(os.path.join(SPRITES, "Tiles", "floor_wood.png"))
    wall_tile(os.path.join(SPRITES, "Tiles", "wall.png"))
    print("restyled", sum(len(f) for _, _, f in os.walk(SOURCE)), "files")


if __name__ == "__main__":
    main()
