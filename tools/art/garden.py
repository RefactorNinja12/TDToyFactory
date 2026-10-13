"""
The garden map (MapTheme.Garden): the lawn, the gravel path through the middle, the flower beds along the
borders and the giant plants lying about. Pictures go to tools/art/source; restyle.py gives them the
night palette (tiles without outlines, plants with).

  Tiles/grass.png        1024x512: the lawn, repeats every 16x8 tiles (like the floorboards)
  Tiles/clover.png       192x24: six clovers and two clover flowers, laid in patches on the lawn by UI/Lawn
  Tiles/garden_path.png  256x256: cobbles in dark soil, repeats every 4x4 tiles (the hall)
  Tiles/dirt.png         576x192: three patches of bare soil, laid on the lawn by UI/FloorDecor
  Tiles/hedge.png        256x256: a trimmed hedge from above, repeats every 4x4 tiles (the walls)
  Obstacles/pumpkin.png, sunflower.png, cabbage.png, carrot.png, flowers.png: footprint + one tile on top
  Obstacles/plantbed.png 320x320: dug soil with stones, drawn under each plant (footprint + half a tile round)
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


# Clover colours from the restyle palette (clover.png keeps them: VIVID), so the leaves don't turn teal.
CLOVER = ((95, 154, 69), (156, 196, 102), (35, 64, 42))   # leaf, light vein and glint, dark rim and notches


def clover_leaf(d, cx, cy, r, rnd):
    """A clover seen from above, like the user's picture: three or four round leaflets with notches between them."""
    leaves = 4 if rnd.random() < 0.3 else 3
    turn = rnd.uniform(0, math.tau)
    leaf, light, dark = CLOVER
    for grow, fill in ((1.5, dark), (0, leaf)):
        for k in range(leaves):
            a = turn + k * math.tau / leaves
            lx, ly = cx + r * 0.5 * math.cos(a), cy + r * 0.5 * math.sin(a)
            lr = r * 0.5 + grow
            d.ellipse((lx - lr, ly - lr, lx + lr, ly + lr), fill=fill)
    for k in range(leaves):
        a = turn + (k + 0.5) * math.tau / leaves                    # the notch between two leaflets
        d.line((cx, cy, cx + r * math.cos(a), cy + r * math.sin(a)), fill=dark)
        a = turn + k * math.tau / leaves                            # the fold down the middle of each leaflet
        d.line((cx + 1.5 * math.cos(a), cy + 1.5 * math.sin(a), cx + r * 0.7 * math.cos(a), cy + r * 0.7 * math.sin(a)), fill=light)


def clover_flower(d, cx, cy):
    """A little white clover flower: five petals round a yellow eye (palette colours, like the leaves)."""
    for k in range(5):
        a = k * math.tau / 5
        px, py = cx + 2.5 * math.cos(a), cy + 2.5 * math.sin(a)
        d.ellipse((px - 2.5, py - 2.5, px + 2.5, py + 2.5), fill=(196, 198, 212))
        d.ellipse((px - 2, py - 2, px + 2, py + 2), fill=(255, 243, 196))
    d.ellipse((cx - 1.5, cy - 1.5, cx + 1.5, cy + 1.5), fill=(242, 182, 74))


def clover():
    """
    Clover lying on the lawn (UI/Lawn spreads it in patches over the tiles): six clovers of different sizes, three
    or four leaflets, then two white clover flowers, each in a 24x24 cell (Lawn.SpriteSize) of a strip.
    """
    cell = 24
    img, d = canvas(8 * cell, cell)
    rnd = random.Random(23)
    for k, r in enumerate((6, 7, 8, 9, 10, 10)):
        clover_leaf(d, k * cell + cell / 2, cell / 2, r, rnd)
    clover_flower(d, 6 * cell + cell / 2, cell / 2)
    clover_leaf(d, 7 * cell + cell / 2 - 3, cell / 2 + 3, 6, rnd)        # a flower peeking out of a clover
    clover_flower(d, 7 * cell + cell / 2 + 3, cell / 2 - 3)
    out(img, "Tiles/clover.png")


def dirt():
    """
    Bare soil on the lawn (UI/FloorDecor puts a few patches in each room): three uneven patches side by side, each
    192x192 (drawn stretched over 2x2 to 3x3 tiles), with crumbs, a few pebbles and grass reaching in at the edge.
    Drawn at half size and blown up with hard pixels.
    """
    size = 3 * T // 2
    sheet = Image.new("RGBA", (3 * size * 2, size * 2))
    for v in range(3):
        rnd = random.Random(40 + v)
        img, d = canvas(size, size)
        soil, dry, wet = (122, 82, 54), (150, 108, 70), (87, 57, 44)
        c = size / 2
        pts = []
        for k in range(40):                                     # an uneven, lumpy edge
            a = k * math.tau / 40
            r = c * (0.8 + 0.08 * math.sin(a * 2 + v) + 0.04 * math.sin(a * 5 + 2 * v) + rnd.uniform(-0.03, 0.03))
            pts.append((c + r * math.cos(a), c + r * math.sin(a)))
        d.polygon(pts, fill=wet)
        d.polygon([(c + (x - c) * 0.9, c + (y - c) * 0.9 - 1) for x, y in pts], fill=soil)
        for _ in range(5):                                      # drier, lighter spots
            x, y, r = rnd.uniform(c * 0.5, c * 1.5), rnd.uniform(c * 0.5, c * 1.5), rnd.uniform(4, 9)
            d.ellipse((x - r, y - r * 0.7, x + r, y + r * 0.7), fill=dry)
        inside = [(x, y) for x in range(size) for y in range(size)
                  if img.getpixel((x, y))[3] and img.getpixel((x, y))[:3] != wet]
        for x, y in rnd.sample(inside, 90):                     # crumbs
            img.putpixel((x, y), (*rnd.choice((wet, dry, tint(soil, 0.2))), 255))
        for x, y in rnd.sample(inside, 6):                      # pebbles
            d.ellipse((x - 2, y - 1, x + 2, y + 2), fill=(110, 110, 128))
            d.point((x - 1, y), fill=(154, 154, 172))
        for x, y in pts[::3]:                                   # grass reaching in over the edge
            for _ in range(3):
                gx, gy = x + rnd.uniform(-3, 3), y + rnd.uniform(-3, 3)
                d.line((gx, gy, gx + rnd.choice((-1, 0, 1)), gy - 3), fill=(58, 107, 53))
        sheet.paste(img.resize((size * 2, size * 2), Image.NEAREST), (v * size * 2, 0))
    out(sheet, "Tiles/dirt.png")


def path():
    """
    The hall's path: cobbles like the user's picture, rounded pale stones of different sizes set close in dark
    soil, lit from the top left, with little pebbles in the gaps. Drawn at half size and blown up with hard pixels;
    the stones wrap round the edges, so the picture repeats every 4x4 tiles without a seam.
    """
    w = h = 4 * T // 2
    rnd = random.Random(5)
    # a little darker and softer than the picture, so deposits, belts and units still stand out on it
    soil, stone, light, shade, rim = (66, 52, 42), (172, 162, 142), (192, 184, 162), (146, 137, 119), (108, 98, 84)
    # stone centres on a jittered grid, each with its own size and tint
    cell = 11
    stones = []
    for gy in range(h // cell):
        for gx in range(w // cell):
            stones.append((gx * cell + rnd.uniform(1, cell - 1), gy * cell + rnd.uniform(1, cell - 1),
                           rnd.uniform(5.5, 8.5), rnd.choice((-0.06, 0, 0, 0.05))))

    def near(px, py):
        """The nearest two stones (wrapping round the picture): (distance, stone), then the second distance."""
        best = second = (1e9, None)
        for st in stones:
            dx = (px - st[0] + w / 2) % w - w / 2
            dy = (py - st[1] + h / 2) % h - h / 2
            d = math.hypot(dx, dy)
            if d < best[0]:
                best, second = (d, (st, dx, dy)), best
            elif d < second[0]:
                second = (d, None)
        return best, second[0]

    img = Image.new("RGBA", (w, h), (*soil, 255))
    px = img.load()
    for y in range(h):
        for x in range(w):
            (d, (st, dx, dy)), d2 = near(x + 0.5, y + 0.5)
            r = st[2]
            if d2 - d < 1.6 or d > r:                    # the gap between stones: soil, now and then a pebble
                if rnd.random() < 0.05:
                    px[x, y] = (*tint(shade, -0.15), 255)
                continue
            lit = (-dx - dy) / (r * 1.41)                # +1 at the top left edge, -1 at the bottom right
            edge = d2 - d < 2.6 or d > r - 1.2
            c = tint(stone, st[3])
            if edge and lit < -0.1:
                c = rim                                  # the shaded lower rim
            elif lit > 0.45:
                c = tint(light, st[3])
            elif lit < -0.35:
                c = tint(shade, st[3])
            px[x, y] = (*c, 255)
    img = img.resize((w * 2, h * 2), Image.NEAREST)
    out(img, "Tiles/garden_path.png")


# Hedge colours from the restyle palette (hedge.png keeps them: VIVID): darker than the lawn, so the walls stand out.
HEDGE = ((35, 64, 42), (58, 107, 53), (95, 154, 69), (156, 196, 102))   # deep shade, leaves, lit leaves, glints


def hedge():
    """
    A trimmed hedge seen from above (the garden's walls): a mat of round leafy clumps, each shaded from the top
    left, dark gaps between them, a few glinting leaves. Repeats every 4x4 tiles without a seam.
    """
    w = h = 4 * T
    rnd = random.Random(17)
    shade, leaf, lit, glint = HEDGE
    img = Image.new("RGBA", (w, h), (*shade, 255))
    d = ImageDraw.Draw(img)
    clumps = [(rnd.uniform(0, w), rnd.uniform(0, h), rnd.uniform(6, 10)) for _ in range(520)]
    for x, y, r in sorted(clumps, key=lambda c: c[1]):
        wrap_ellipse(d, w, h, (x - r + 1, y - r + 2, x + r + 1, y + r + 2), shade)                 # its shadow
        wrap_ellipse(d, w, h, (x - r, y - r, x + r, y + r), leaf)
        wrap_ellipse(d, w, h, (x - r * 0.75, y - r * 0.8, x + r * 0.25, y + r * 0.1), lit)        # lit top left
    for _ in range(700):                                                                          # single leaves
        x, y = rnd.randrange(w), rnd.randrange(h)
        c = rnd.choice((lit, lit, glint, shade))
        wrap_ellipse(d, w, h, (x - 1, y - 1, x + 1, y + 1), c)
    out(img, "Tiles/hedge.png")


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


def cut_shadow(pic):
    """
    Cuts a painted shadow (grey or dark blue-grey) and light halos away from a picture, in place: only such
    pixels reached from the outside, so dark grey-greens between the leaves inside stay. Then the specks left over.
    """
    px = pic.load()
    w, h = pic.size

    def shadowy(x, y):
        r, g, b, a = px[x, y]
        spread = max(r, g, b) - min(r, g, b)
        grey = spread < 34 and max(r, g, b) > 70                       # the shadow, a halo
        bluish = b >= r - 6 and spread < 44 and max(r, g, b) > 30       # the shadow's dark rim
        return a < 128 or grey or bluish

    todo = [(x, y) for x in range(w) for y in (0, h - 1)] + [(x, y) for y in range(h) for x in (0, w - 1)]
    todo = [p for p in todo if shadowy(*p)]
    outside = set(todo)
    while todo:
        x, y = todo.pop()
        for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= n[0] < w and 0 <= n[1] < h and n not in outside and shadowy(*n):
                outside.add(n)
                todo.append(n)
    for x, y in outside:
        px[x, y] = (0, 0, 0, 0)
    drop_specks(pic)


def drop_specks(pic, smallest=40):
    """Clears little islands of pixels (bits of a cut-away shadow or halo) from a picture, in place."""
    px = pic.load()
    seen = set()
    for y0 in range(pic.height):
        for x0 in range(pic.width):
            if (x0, y0) in seen or px[x0, y0][3] < 128:
                continue
            island, todo = [], [(x0, y0)]
            seen.add((x0, y0))
            while todo:
                x, y = todo.pop()
                island.append((x, y))
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if 0 <= nx < pic.width and 0 <= ny < pic.height and (nx, ny) not in seen and px[nx, ny][3] >= 128:
                        seen.add((nx, ny))
                        todo.append((nx, ny))
            if len(island) < smallest:
                for x, y in island:
                    px[x, y] = (0, 0, 0, 0)


def painting(name, w, h, colours=32, greens=True, shadow=False, pixel=2):
    """
    A finished picture with a transparent background (the user's own sprite, tools/art/reference/<name>.png) made
    to fit the game without losing its detail: fitted on the footprint (standing on the bottom), half resolution
    with hard pixels and flat colours. Its greens (a stem) become the palette's greens, which otherwise turn brown
    or into outline black; the real black lines stay (greens=False: left as they are). shadow=True: the picture
    has a painted grey shadow (and light halos), cut away; the game casts its own shadows (no shadow without light).
    pixel=1: full resolution (only fitted), for a picture whose small details matter.
    """
    pic = Image.open(os.path.join(REFERENCE, name + ".png")).convert("RGBA")
    if shadow:
        cut_shadow(pic)
    pic = pic.crop(pic.getchannel("A").point(lambda a: 255 if a >= 128 else 0).getbbox())
    scale = min(w / pic.width, h / pic.height)
    pic = pic.resize((round(pic.width * scale), round(pic.height * scale)), Image.LANCZOS)
    fitted, _ = canvas(w, h)
    fitted.alpha_composite(pic, ((w - pic.width) // 2, h - pic.height))
    small = fitted.resize((w // pixel, h // pixel), Image.LANCZOS)
    flat = pixel_art(small, pixel, colours)
    if not greens:
        return flat
    src, dst = small.load(), flat.load()
    for y in range(small.height):
        for x in range(small.width):
            r, g, b, a = src[x, y]
            lum = 0.299 * r + 0.587 * g + 0.114 * b
            if a < 128 or lum < 30 or g < r * 0.9 or g < b:
                continue
            green = (58, 107, 53) if lum < 80 else (95, 154, 69)
            for dy in range(pixel):
                for dx in range(pixel):
                    dst[pixel * x + dx, pixel * y + dy] = (*green, 255)
    return flat


def pumpkin():
    if os.path.exists(os.path.join(REFERENCE, "pumpkin_top.png")):
        # The user's own pumpkin seen from above (ribs, speckles, twisted stem), on its 4x4 footprint + one tile.
        out(painting("pumpkin_top", 4 * T, 5 * T), "Obstacles/pumpkin.png")
        return
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
    if os.path.exists(os.path.join(REFERENCE, "sunflower.png")):
        # The user's own sunflower, on its 3x4 footprint + one tile; just fitted (its leaves are green enough).
        out(painting("sunflower", 3 * T, 5 * T, colours=64, greens=False), "Obstacles/sunflower.png")
        return
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
    if os.path.exists(os.path.join(REFERENCE, "cabbage_top.png")):
        # The user's own finished cabbage, on its 3x3 footprint + one tile; just fitted.
        out(painting("cabbage_top", 3 * T, 4 * T, colours=64, greens=False), "Obstacles/cabbage.png")
        return
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
    if os.path.exists(os.path.join(REFERENCE, "carrot.png")):
        # The user's own carrot lying on the lawn, on its 5x3 footprint + one tile; its painted shadow cut away.
        out(painting("carrot", 5 * T, 4 * T, colours=64, greens=False, shadow=True), "Obstacles/carrot.png")
        return
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


def plantbed():
    """
    The user's bed of dug soil with stones (tools/art/reference/plantbed.png, on white): drawn under every giant
    plant by ObstacleView, stretched over its footprint and a little round it, so the plants look planted. The white
    background and the soft grey shadow are cut away, as are the loose crumbs round the edge.
    """
    if os.path.exists(os.path.join(REFERENCE, "plantbed.png")):
        out(painting("plantbed", 5 * T, 5 * T, colours=48, greens=False, shadow=True), "Obstacles/plantbed.png")


def flowers():
    if os.path.exists(os.path.join(REFERENCE, "flowers_top.png")):
        # The user's own flower patch: its painted shadow cut away, fitted at full resolution (small flowers).
        out(painting("flowers_top", 3 * T, 3 * T, colours=96, greens=False, shadow=True, pixel=1), "Obstacles/flowers.png")
        return
    out(flower_patch(3 * T, 3 * T), "Obstacles/flowers.png")


if __name__ == "__main__":
    grass()
    clover()
    path()
    dirt()
    hedge()
    pumpkin()
    sunflower()
    cabbage()
    carrot()
    flowers()
    plantbed()
    print(" ".join(WRITTEN))
