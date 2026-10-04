"""
The garden map (MapTheme.Garden): the lawn, the gravel path through the middle, the flower beds along the
borders and the giant plants lying about. Pictures go to tools/art/source; restyle.py gives them the
night palette (tiles without outlines, plants with).

  Tiles/grass.png        1024x512: the lawn, repeats every 16x8 tiles (like the floorboards)
  Tiles/garden_path.png  256x256: gravel with stepping stones, repeats every 4x4 tiles (the hall)
  Tiles/flowerbed.png    64x64: a raised wooden planter with soil and flowers (the walls)
  Obstacles/pumpkin.png, sunflower.png, cabbage.png, carrot.png, tulips.png: footprint + one tile on top
  (seen slanted from above, like the toys: ObstacleView draws one tile of overhang)

    python tools/art/garden.py && python tools/art/restyle.py <the files it lists>
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit import *  # noqa: E402,F403

T = 64
WRITTEN = []
GRASS = (92, 160, 74)
LEAF = (78, 150, 70)


def out(img, rel):
    save(img, rel)
    WRITTEN.append(rel)


def wrap_ellipse(d, w, h, box, fill):
    """An ellipse drawn again across the edges, so the picture repeats without a seam."""
    x0, y0, x1, y1 = box
    for dx in (-w, 0, w):
        for dy in (-h, 0, h):
            d.ellipse((x0 + dx, y0 + dy, x1 + dx, y1 + dy), fill=fill)


def grass():
    w, h = 16 * T, 8 * T
    rnd = random.Random(11)
    img = Image.new("RGBA", (w, h), (*GRASS, 255))
    d = ImageDraw.Draw(img)
    # faint mowing stripes, two tiles wide
    for x in range(0, w, 4 * T):
        d.rectangle((x, 0, x + 2 * T - 1, h - 1), fill=tint(GRASS, 0.05))
    # darker and lighter patches
    for _ in range(140):
        cx, cy, r = rnd.randrange(w), rnd.randrange(h), rnd.randrange(10, 40)
        wrap_ellipse(d, w, h, (cx - r, cy - r // 2, cx + r, cy + r // 2), tint(GRASS, rnd.choice((-0.12, -0.08, 0.08))))
    # blades: short strokes, lighter on top
    for _ in range(9000):
        x, y = rnd.randrange(w), rnd.randrange(h)
        lean = rnd.choice((-1, 0, 1))
        color = tint(GRASS, rnd.choice((-0.25, -0.15, 0.15, 0.25)))
        for dx in (0, -w):                       # strokes over the edge go on at the other side
            for dy in (0, h):
                d.line((x + dx, y + dy, x + dx + lean, y + dy - 3), fill=color)
    # clover and a few daisies
    for _ in range(60):
        x, y = rnd.randrange(w), rnd.randrange(h)
        for a in range(3):
            ang = a * 2.1
            px, py = int(x + 3 * math.cos(ang)) % w, int(y + 3 * math.sin(ang)) % h
            d.ellipse((px - 2, py - 2, px + 2, py + 2), fill=tint(GRASS, -0.2))
    for _ in range(45):
        x, y = rnd.randrange(4, w - 4), rnd.randrange(4, h - 4)
        for a in range(6):
            ang = a * math.pi / 3
            d.point((x + round(2 * math.cos(ang)), y + round(2 * math.sin(ang))), fill=WHITE)
        d.point((x, y), fill=YELLOW)
    out(img, "Tiles/grass.png")


def path():
    w = h = 4 * T
    rnd = random.Random(5)
    gravel = (176, 160, 134)
    img = Image.new("RGBA", (w, h), (*gravel, 255))
    d = ImageDraw.Draw(img)
    for _ in range(2600):
        x, y = rnd.randrange(w), rnd.randrange(h)
        d.point((x, y), fill=tint(gravel, rnd.choice((-0.3, -0.18, 0.15))))
    # stepping stones, two per tile row, offset
    stone = (150, 150, 162)
    for row in range(4):
        for col in range(2):
            cx = col * 2 * T + T + (T // 2 if row % 2 else 0)
            cy = row * T + T // 2
            r = 22 + rnd.randrange(5)
            wrap_ellipse(d, w, h, (cx - r, cy - r + 4, cx + r, cy + r - 2), tint(stone, -0.3))
            wrap_ellipse(d, w, h, (cx - r + 1, cy - r + 3, cx + r - 2, cy + r - 4), stone)
            wrap_ellipse(d, w, h, (cx - r // 2, cy - r // 2, cx, cy - r // 4), tint(stone, 0.25))
    out(img, "Tiles/garden_path.png")


def flowerbed():
    img, d = canvas(T, T)
    rnd = random.Random(3)
    plank = (150, 104, 64)
    soil = (96, 66, 50)
    d.rectangle((0, 0, T - 1, T - 1), fill=plank)            # the planter's wooden edge all round
    for y in (0, T - 7):
        d.line((0, y, T - 1, y), fill=tint(plank, 0.3))
    d.rectangle((6, 6, T - 7, T - 7), fill=soil)
    for _ in range(60):
        d.point((rnd.randrange(7, T - 7), rnd.randrange(7, T - 7)), fill=tint(soil, rnd.choice((-0.25, 0.2))))
    # flowers in the bed: leaves, then a little five-petal flower each
    colors = [RED, YELLOW, (240, 140, 200), (140, 120, 230), WHITE, ORANGE]
    for i, (fx, fy) in enumerate(((16, 16), (34, 14), (50, 22), (22, 34), (42, 38), (14, 50), (32, 50), (50, 48))):
        d.ellipse((fx - 5, fy - 2, fx + 1, fy + 3), fill=LEAF)
        d.ellipse((fx - 1, fy - 1, fx + 5, fy + 4), fill=tint(LEAF, -0.15))
        c = colors[i % len(colors)]
        for a in range(5):
            ang = a * 2 * math.pi / 5
            px, py = fx + 3 * math.cos(ang), fy - 2 + 3 * math.sin(ang)
            d.ellipse((px - 2, py - 2, px + 2, py + 2), fill=c)
        d.point((fx, fy - 2), fill=YELLOW if c != YELLOW else ORANGE)
    out(img, "Tiles/flowerbed.png")


# ---- giant plants: footprint (w x h tiles) plus one tile of height on top ----

def pumpkin():
    w, h = 4 * T, 5 * T
    img, d = canvas(w, h)
    body = (236, 132, 48)
    cx, cy, rx, ry = w // 2, h // 2 + 40, 118, 104
    d.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=tint(body, -0.25))
    d.ellipse((cx - rx + 4, cy - ry + 2, cx + rx - 6, cy + ry - 8), fill=body)
    # the ribs: narrow ovals side by side, kept inside the pumpkin
    ribs, r = canvas(w, h)
    for k in (-2, -1, 0, 1, 2):
        x = cx + k * rx * 0.36
        half = rx * (0.22 if k else 0.3)
        r.ellipse((x - half, cy - ry + 8, x + half, cy + ry - 12), outline=tint(body, -0.3), width=3)
    mask, m = canvas(w, h)
    m.ellipse((cx - rx + 6, cy - ry + 4, cx + rx - 8, cy + ry - 10), fill=(255, 255, 255, 255))
    img.paste(ribs, (0, 0), Image.composite(ribs, Image.new("RGBA", ribs.size), mask.split()[3]))
    d.ellipse((cx - 60, cy - ry + 18, cx - 20, cy - ry + 40), fill=tint(body, 0.4))   # shine
    d.rectangle((cx - 9, cy - ry - 26, cx + 9, cy - ry + 10), fill=(96, 120, 52))     # the stem
    d.ellipse((cx - 12, cy - ry - 32, cx + 12, cy - ry - 18), fill=(120, 146, 64))
    d.line([(cx + 8, cy - ry - 10), (cx + 40, cy - ry - 30), (cx + 60, cy - ry - 10)], fill=LEAF, width=4)
    d.ellipse((cx + 40, cy - ry - 52, cx + 96, cy - ry - 10), fill=LEAF)              # a leaf
    out(img, "Obstacles/pumpkin.png")


def sunflower():
    w, h = 3 * T, 5 * T
    img, d = canvas(w, h)
    cx = w // 2
    d.rectangle((cx - 8, 150, cx + 8, h - 8), fill=(84, 140, 60))                     # the stem
    for y, side in ((210, -1), (260, 1)):                                            # leaves
        d.ellipse((cx + side * 10 - (60 if side < 0 else 0), y - 18, cx + side * 10 + (0 if side < 0 else 60), y + 18), fill=LEAF)
        d.line((cx, y, cx + side * 54, y - 4), fill=tint(LEAF, -0.3), width=2)
    hy, r = 96, 82
    for k in range(18):                                                              # petals
        a = k * math.tau / 18
        px, py = cx + (r - 18) * math.cos(a), hy + (r - 18) * math.sin(a)
        d.ellipse((px - 18, py - 18, px + 18, py + 18), fill=YELLOW if k % 2 else tint(YELLOW, -0.12))
    d.ellipse((cx - 44, hy - 44, cx + 44, hy + 44), fill=(110, 70, 40))             # the seeds
    for k in range(60):
        a, rr = k * 2.39996, 3.6 * math.sqrt(k)
        d.point((cx + rr * math.cos(a), hy + rr * math.sin(a)), fill=(70, 44, 28))
    out(img, "Obstacles/sunflower.png")


def cabbage():
    w, h = 3 * T, 4 * T
    img, d = canvas(w, h)
    cx, cy = w // 2, h // 2 + 30
    green = (120, 176, 96)
    shades = [(84, 140, 66), (104, 160, 78), (128, 182, 90), (150, 198, 100), (170, 210, 112)]
    for k, r in enumerate((92, 78, 62, 46, 30)):                                     # layers of leaves
        c = shades[k]
        for a in range(7):
            ang = a * math.tau / 7 + k * 0.4
            px, py = cx + r * 0.45 * math.cos(ang), cy + r * 0.38 * math.sin(ang)
            d.ellipse((px - r * 0.6, py - r * 0.5, px + r * 0.6, py + r * 0.5), fill=c)
        d.arc((cx - r, cy - r * 0.8, cx + r, cy + r * 0.8), 200, 340, fill=tint(c, -0.3), width=2)
    d.ellipse((cx - 16, cy - 14, cx + 16, cy + 12), fill=(176, 214, 120))
    out(img, "Obstacles/cabbage.png")


def carrot():
    w, h = 5 * T, 4 * T
    img, d = canvas(w, h)
    body = (240, 130, 40)
    cy = h // 2 + 34
    # lying down, the green top on the left, pointing right
    d.polygon([(70, cy - 52), (w - 20, cy - 6), (w - 20, cy + 6), (70, cy + 52)], fill=tint(body, -0.25))
    d.polygon([(72, cy - 48), (w - 26, cy - 4), (72, cy + 40)], fill=body)
    d.ellipse((46, cy - 54, 110, cy + 54), fill=body)
    for x in range(110, w - 40, 34):                                                 # rings
        t = (x - 70) / (w - 90)
        half = int(52 * (1 - t))
        d.line((x, cy - half + 4, x + 4, cy + half - 6), fill=tint(body, -0.3), width=3)
    for k in range(5):                                                               # the leafy top
        a = math.pi + (k - 2) * 0.35
        x1, y1 = 56 + 70 * math.cos(a), cy + 70 * math.sin(a) - 10
        d.line((58, cy, x1, y1), fill=(70, 140, 60), width=6)
        d.ellipse((x1 - 12, y1 - 10, x1 + 12, y1 + 10), fill=LEAF)
    out(img, "Obstacles/carrot.png")


def tulips():
    w, h = 3 * T, 3 * T
    img, d = canvas(w, h)
    base = h - 24
    for x, top, color in ((50, 52, RED), (96, 34, YELLOW), (142, 58, (236, 120, 190))):
        d.line((x, top + 24, x, base), fill=(84, 140, 60), width=6)                  # stem
        d.ellipse((x - 26, base - 50, x + 2, base - 4), fill=LEAF)                    # leaves
        d.ellipse((x - 2, base - 40, x + 24, base - 4), fill=tint(LEAF, -0.15))
        d.polygon([(x - 20, top + 6), (x - 12, top - 10), (x, top + 2), (x + 12, top - 10), (x + 20, top + 6),
                   (x + 16, top + 30), (x - 16, top + 30)], fill=color)              # the cup
        d.line((x, top + 4, x, top + 28), fill=tint(color, -0.25), width=2)
    out(img, "Obstacles/tulips.png")


if __name__ == "__main__":
    grass()
    path()
    flowerbed()
    pumpkin()
    sunflower()
    cabbage()
    carrot()
    tulips()
    print(" ".join(WRITTEN))
