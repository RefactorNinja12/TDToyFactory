"""
The garden map (MapTheme.Garden): the lawn, the gravel path through the middle, the flower beds along the
borders and the giant plants lying about. Pictures go to tools/art/source; restyle.py gives them the
night palette (tiles without outlines, plants with).

  Tiles/grass.png        1024x512: the lawn, repeats every 16x8 tiles (like the floorboards)
  Tiles/garden_path.png  256x256: gravel with stepping stones, repeats every 4x4 tiles (the hall)
  Tiles/flowerbed.png    64x64: a raised wooden planter with soil and flowers (the walls)
  Obstacles/pumpkin.png, sunflower.png, cabbage.png, carrot.png, flowers.png: footprint + one tile on top
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
    if rel in HAND_EDITED:
        return
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
    gravel = (150, 134, 110)
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

REFERENCE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "reference")


def from_reference(name, w, h, pixel=2, colours=16):
    """
    A picture made from a reference drawing (tools/art/reference/<name>.png, e.g. one the user supplied), made
    to fit the game's look: the light background and its grey drop shadow are cut away (filled from the edges,
    stopped by the dark outline; the game works out shadows itself); it is scaled to fit w x h standing on the
    bottom, at 1/<pixel> size and blown up again with hard edges (pixels as chunky as the other sprites), with
    a few flat colours and solid edges. restyle.py then puts it in the night palette with black outlines.
    """
    from collections import deque
    src = Image.open(os.path.join(REFERENCE, f"{name}.png")).convert("RGBA")
    sw, sh = src.size
    px = src.load()

    def background(c):
        r, g, b, _ = c
        return min(r, g, b) > 196 and max(r, g, b) - min(r, g, b) < 34

    seen = bytearray(sw * sh)
    queue = deque((x, y) for x in range(sw) for y in (0, sh - 1))
    queue.extend((x, y) for y in range(sh) for x in (0, sw - 1))
    while queue:
        x, y = queue.popleft()
        if x < 0 or y < 0 or x >= sw or y >= sh or seen[y * sw + x] or not background(px[x, y]):
            continue
        seen[y * sw + x] = 1
        px[x, y] = (0, 0, 0, 0)
        queue.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    # Holes of background shut in by the stem and leaves (top part only: the eyes further down are white too).
    for y in range(int(sh * 0.35)):
        for x in range(sw):
            if px[x, y][3] and background(px[x, y]):
                px[x, y] = (0, 0, 0, 0)
    cut = src.crop(src.getbbox())
    sw, sh = w // pixel, h // pixel
    scale = min(sw / cut.width, sh / cut.height)
    cut = cut.resize((max(1, round(cut.width * scale)), max(1, round(cut.height * scale))), Image.LANCZOS)
    small, _ = canvas(sw, sh)
    small.alpha_composite(cut, ((sw - cut.width) // 2, sh - cut.height))
    return pixel_art(small, pixel, colours)


def pixel_art(small, pixel, colours):
    """A small picture made to look like the other sprites: flat colours, solid edges, blown up with hard pixels."""
    alpha = small.getchannel("A").point(lambda a: 255 if a >= 128 else 0)
    # octree keeps small areas' hues (a median cut gave the pumpkin's green stem away to its orange)
    flat = small.convert("RGB").quantize(colours, method=Image.Quantize.FASTOCTREE).convert("RGB")
    flat.putalpha(alpha)
    return flat.resize((small.width * pixel, small.height * pixel), Image.NEAREST)


def petal(d, cx, cy, ang, inner, outer, width, fill):
    """One petal: a pointed oval from inner to outer along the angle."""
    pts = []
    for k in range(13):
        t = k / 12
        r = inner + (outer - inner) * t
        half = width * math.sin(math.pi * t) ** 0.7
        pts.append((t, r, half))
    left = [(cx + r * math.cos(ang) - h * math.sin(ang), cy + r * math.sin(ang) + h * math.cos(ang)) for _, r, h in pts]
    right = [(cx + r * math.cos(ang) + h * math.sin(ang), cy + r * math.sin(ang) - h * math.cos(ang)) for _, r, h in pts]
    d.polygon(left + right[::-1], fill=fill)


# Flower colours picked from the restyle palette, so the night muting can't turn the white grey or the leaves into lawn.
PETALS = {
    "daisy": ((255, 243, 196), (196, 198, 212)),
    "orange": ((227, 139, 53), (242, 182, 74)),
    "red": ((184, 56, 74), (224, 100, 106)),
    "pink": ((201, 140, 150), (224, 100, 106)),
    "yellow": ((242, 182, 74), (247, 221, 122)),
}
EYE = ((242, 182, 74), (196, 96, 42))


def bloom(d, cx, cy, r, kind, rng):
    """
    One flower seen from above, like the user's meadow picture: a daisy (many narrow petals round a yellow eye)
    or a round bloom (broad petals, a darker heart). Each petal has a darker rim, so the petals stay apart.
    """
    turn = rng.uniform(0, math.tau)
    colour, other = PETALS[kind]
    if kind == "daisy":
        for k in range(10):
            a = turn + k * math.tau / 10
            petal(d, cx, cy, a, r * 0.2, r + 1, r * 0.3, other)                     # the rim between petals
            petal(d, cx, cy, a, r * 0.2, r - 1, r * 0.13, colour)
    else:
        petals = 10 if kind in ("orange", "yellow") else 6
        width = 0.36 if petals == 10 else 0.6
        for k in range(petals):
            a = turn + k * math.tau / petals
            petal(d, cx, cy, a, 0, r + 1, r * (width + 0.08), tint(colour, -0.35))
            petal(d, cx, cy, a, 0, r - 1, r * width, colour)
        for k in range(petals):                                   # the inner half of each petal lighter
            a = turn + k * math.tau / petals
            petal(d, cx, cy, a, 0, r * 0.55, r * width * 0.55, other)
    eye, ring = EYE if kind != "yellow" else ((196, 96, 42), (138, 59, 31))
    d.ellipse((cx - r * 0.32, cy - r * 0.32, cx + r * 0.32, cy + r * 0.32), fill=ring)
    d.ellipse((cx - r * 0.26, cy - r * 0.28, cx + r * 0.22, cy + r * 0.2), fill=eye)


def flower_patch(w, h, pixel=2):
    """
    A patch of flowers on the lawn, painted after the user's meadow picture (tools/art/reference/flowers.png):
    a wavy mound of pointed leaves in several greens, with a big white daisy among smaller daisies and orange,
    red, pink and yellow blooms. Painted at full size, then made into chunky pixels like the other plants.
    """
    rng = random.Random(7)
    img, d = canvas(w, h)
    cx, cy, rx, ry = w / 2, h * 0.56, w * 0.47, h * 0.41

    def inside(x, y, shrink=1.0):
        return ((x - cx) / (rx * shrink)) ** 2 + ((y - cy) / (ry * shrink)) ** 2 <= 1

    # the leafy mound: a dark ground, then layers of leaves, darker underneath and lighter on top
    pts = []
    for k in range(48):
        a = k * math.tau / 48
        wobble = 1 + 0.05 * math.sin(a * 7) + 0.04 * math.sin(a * 11 + 1)
        pts.append((cx + rx * wobble * math.cos(a), cy + ry * wobble * math.sin(a)))
    d.polygon(pts, fill=(35, 64, 42))
    greens = [(58, 107, 53), (58, 107, 53), (95, 154, 69), (156, 196, 102)]
    for layer, green in enumerate(greens):
        for _ in range(70 - layer * 10):
            x, y = rng.uniform(cx - rx, cx + rx), rng.uniform(cy - ry, cy + ry)
            if not inside(x, y, 0.98 - layer * 0.04):
                continue
            a = rng.uniform(0, math.tau)
            length = rng.uniform(16, 26)
            petal(d, x, y, a, 0, length + 2, length * 0.3 + 1, (35, 64, 42))   # dark rim
            petal(d, x, y, a, 0, length, length * 0.28, green)
            # the leaf's middle vein
            d.line((x, y, x + length * 0.8 * math.cos(a), y + length * 0.8 * math.sin(a)), fill=(35, 64, 42), width=1)

    # the flowers: the big daisy a little off centre, then the others spread round it without piling up
    placed = [(cx + 6, cy - 4, 30, "daisy")]
    kinds = ["daisy", "orange", "red", "pink", "daisy", "orange", "red", "yellow", "daisy", "pink", "red", "orange"]
    tries = 0
    while len(placed) < 16 and tries < 4000:
        tries += 1
        r = rng.choice((10, 12, 14, 16))
        x, y = rng.uniform(cx - rx, cx + rx), rng.uniform(cy - ry, cy + ry)
        if not inside(x, y, 0.86) or any(math.hypot(x - px, y - py) < (r + pr) * 0.85 for px, py, pr, _ in placed):
            continue
        placed.append((x, y, r, kinds[len(placed) % len(kinds)]))
    for x, y, r, kind in sorted(placed, key=lambda f: f[1]):          # back to front
        bloom(d, x, y, r, kind, rng)
    small = img.resize((w // pixel, h // pixel), Image.LANCZOS)
    return pixel_art(small, pixel, 28)


def pumpkin():
    if os.path.exists(os.path.join(REFERENCE, "pumpkin.png")):
        # The user's pumpkin (smiling face, curly vine, leaf), fitted on its 4x4 footprint + one tile of height.
        out(from_reference("pumpkin", 4 * T, 5 * T), "Obstacles/pumpkin.png")
        return
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
    if os.path.exists(os.path.join(REFERENCE, "cabbage.png")):
        out(from_reference("cabbage", 3 * T, 4 * T), "Obstacles/cabbage.png")
        return
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


def flowers():
    out(flower_patch(3 * T, 3 * T), "Obstacles/flowers.png")


if __name__ == "__main__":
    grass()
    path()
    flowerbed()
    pumpkin()
    sunflower()
    cabbage()
    carrot()
    flowers()
    print(" ".join(WRITTEN))
