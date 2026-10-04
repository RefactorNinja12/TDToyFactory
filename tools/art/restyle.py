"""
Restyles every sprite into one look: a shared muted night palette (fits the navy fog), near-black
outlines, a little less saturated and "cartoony"; and generates the dark wooden floor (big boards, as seen by a toy).

    python tools/art/restyle.py                          (everything, and the floor)
    python tools/art/restyle.py Buildings/kitchen.png ... (just those)

Reads the originals from tools/art/source/ (copied there from factory-td/Assets/Sprites on the first
run) and writes factory-td/Assets/Sprites/, so it can be re-run with a tweaked palette without
degrading the art. Tiles/floor_wood.png is generated from scratch (a 16x8 tile picture of big boards); the wall stays as drawn.
"""
import os
import random
import shutil
from PIL import Image

from kit import HAND_EDITED

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
    # reds / pinks (the last two: mouse ears, paws and tails)
    "4a1a2a", "7e2536", "b8384a", "e0646a", "c98c96", "9a5c6c",
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


# Pictures that stay bright: cheese is the game's warm yellow accent; the flower patch is painted in palette colours.
VIVID = {"cheese.png", "melted_cheese.png", "cheese_melter.png", "flowers.png", "clover.png"}
# Pictures muted only a little: their warm colours turn brown and their dark greens grey at the full muting
# (the pumpkin made from the user's drawing).
SOFT = {"pumpkin.png"}


def muted(c, vivid=False):
    """A little less saturated and a little darker: less cartoon, more night (vivid: barely)."""
    r, g, b = c
    grey = 0.299 * r + 0.587 * g + 0.114 * b
    if vivid == "soft":
        k, dark = 0.85, 0.92
    elif vivid:
        return tuple(max(0, min(255, int(grey + (v - grey) * 0.95))) for v in (r, g, b))
    else:
        k, dark = 0.62, 0.86  # keep 62% of the saturation, a little darker
    r, g, b = (grey + (v - grey) * k for v in (r, g, b))
    return tuple(max(0, min(255, int(v * dark))) for v in (r, g, b))


def nearest(c, vivid=False):
    key = (c, vivid)
    if key in _cache:
        return _cache[key]
    lab = srgb_to_lab(muted(c, vivid))
    best = min(range(len(PALETTE)), key=lambda i: sum((a - b) ** 2 for a, b in zip(lab, PALETTE_LAB[i])))
    _cache[key] = PALETTE[best]
    return PALETTE[best]


def luminance(c):
    return (0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]) / 255.0


def restyle(path_in, path_out, outline=True, vivid=False):
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
                dst[x, y] = (8, 8, 24, a) if luminance((r, g, b)) < 0.3 else (*nearest((r, g, b), vivid), a)
                continue
            edge = outline and any(not solid(x + dx, y + dy) and 0 <= x + dx < w and 0 <= y + dy < h
                                   for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if edge or luminance((r, g, b)) < 0.2:
                dst[x, y] = (*OUTLINE, 255)  # clear black outlines (and the old dark ink lines)
            else:
                dst[x, y] = (*nearest((r, g, b), vivid), 255)
    out.save(path_out)


FLOOR_TILES_X, FLOOR_TILES_Y = 16, 8  # the floor picture spans this many map tiles, then repeats


def floor_tiles(path_out, tile=64):
    """
    Big dark floorboards, as a room looks to a toy: boards two tiles wide and 5-16 tiles long, in a picture
    of FLOOR_TILES_X x FLOOR_TILES_Y tiles that repeats seamlessly (seams wrap around). The map picks the
    part of the picture by tile position, so boards run on across tiles.
    """
    rng = random.Random(1234)
    woods = [(58, 40, 33), (66, 45, 36), (74, 50, 38), (62, 43, 36), (52, 37, 31), (70, 47, 35)]
    seam = (22, 15, 18)
    width, height = FLOOR_TILES_X * tile, FLOOR_TILES_Y * tile
    board = tile * 2
    img = Image.new("RGBA", (width, height))
    px = img.load()
    for row in range(height // board):
        # Board ends in this row, around a loop (the last board continues at the start of the row).
        cuts = sorted(rng.sample(range(0, width, 8), rng.choice((1, 2, 2, 3))))
        for k, start in enumerate(cuts):
            end = cuts[k + 1] if k + 1 < len(cuts) else cuts[0] + width
            base = rng.choice(woods)
            shift = rng.randint(-3, 3)
            # Grain: a few long wavy lines along the board, and faint streaks.
            lines = [(rng.randrange(6, board - 6), rng.uniform(0.0, 6.28), rng.choice((-9, -6, 5))) for _ in range(rng.randint(5, 8))]
            knots = [(start + rng.randrange(20, max(21, end - start - 20)), rng.randrange(18, board - 18))
                     for _ in range(rng.choice((0, 0, 1, 2)))]
            for gx in range(start, end):
                x = gx % width
                lx = gx - start
                for ly in range(board):
                    y = row * board + ly
                    c = [ch + shift for ch in base]
                    for gy, phase, dc in lines:
                        wave = gy + 2.0 * __import__("math").sin(lx / 37.0 + phase)
                        if abs(ly - wave) < 0.8 and (lx * 5 + gy) % 29 != 0:
                            c = [ch + dc for ch in c]
                    if (lx * 3 + ly * 17) % 53 == 0:
                        c = [ch - 4 for ch in c]  # fine speckle
                    for kx, ky in knots:
                        d = ((gx - kx) / 2.2) ** 2 + (ly - ky) ** 2
                        if d <= 9:
                            c = [ch - 18 for ch in c]
                        elif d <= 20:
                            c = [ch - 7 for ch in c]  # ring around the knot
                    if ly in (2, 3):
                        c = [ch + 10 for ch in c]  # bevel: light on the top edge
                    elif ly >= board - 3:
                        c = [ch - 9 for ch in c]  # shadow on the bottom edge
                    if ly < 2 or lx < 2:
                        c = list(seam)  # gaps between boards
                    px[x, y] = (*[max(0, min(255, int(ch))) for ch in c], 255)
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


def main(only=None):
    """only: source-relative paths (e.g. Buildings/assembler.png) to restyle just those; None = all + floor."""
    if not os.path.isdir(SOURCE):
        shutil.copytree(SPRITES, SOURCE, ignore=shutil.ignore_patterns("*.import"))
    wanted = {os.path.normpath(p) for p in only} if only else None
    for folder, _, files in os.walk(SOURCE):
        rel = os.path.relpath(folder, SOURCE)
        for name in files:
            if not name.endswith(".png"):
                continue
            if wanted is not None and os.path.normpath(os.path.join(rel, name)) not in wanted:
                continue
            if os.path.join(rel, name).replace(os.sep, "/") in HAND_EDITED:
                continue  # finished by hand
            src = os.path.join(folder, name)
            dst = os.path.join(SPRITES, rel, name)
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            if rel == "Tiles" and name == "floor_wood.png":
                continue
            if rel == "Tiles" and name == "wall.png":
                shutil.copyfile(src, dst)  # the wall keeps its original look
                continue
            # Floor tiles get no outline. Everything else does, but never along the picture's own border
            # (see `restyle`), so belts still join their neighbours without a seam.
            restyle(src, dst, outline=rel != "Tiles", vivid="soft" if name in SOFT else name in VIVID)
    if wanted is not None:
        print("restyled", len(wanted), "files")
        return
    floor_tiles(os.path.join(SPRITES, "Tiles", "floor_wood.png"))
    print("restyled", sum(len(f) for _, _, f in os.walk(SOURCE)), "files")


if __name__ == "__main__":
    import sys
    main(sys.argv[1:] or None)
