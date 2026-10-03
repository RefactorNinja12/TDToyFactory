"""
Drawing kit for the code-drawn sprites: toy plastic shapes with a lit top-left and a shaded bottom-right,
brick studs, wood, gears, little mice with hats. Pictures are drawn at game size (64 px per tile) with
rough colours; restyle.py maps them to the night palette and adds the black outline afterwards, so
nothing here draws outlines on the silhouette (dark "ink" lines inside are fine: luminance < 0.2 turns
pure outline-black).

    from kit import *
    img, d = canvas(64, 64)
    baseplate(img, d); box(d, (10, 10, 54, 54), RED); save(img, "Buildings/x.png")
"""
import math
import os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.join(HERE, "source")

# Rough toy colours (restyle picks the nearest palette colour after muting them).
RED = (214, 64, 70)
BLUE = (70, 120, 210)
YELLOW = (246, 196, 72)
GREEN = (96, 170, 84)
ORANGE = (232, 130, 52)
PURPLE = (150, 96, 180)
TEAL = (70, 170, 150)
WHITE = (236, 238, 244)
GREY = (150, 154, 170)
DARK = (70, 72, 90)
WOOD = (176, 122, 70)
CHEESE = (246, 200, 80)
PINK = (236, 140, 150)
MOUSE = (176, 176, 190)
INK = (20, 16, 28)        # turns into outline black
GLASS = (70, 150, 255)
CELL = (96, 108, 140)     # battery casing (darker colours would turn into outline black)
BASE = (96, 150, 92)      # the green baseplate every factory stands on


def canvas(w, h):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def save(img, rel):
    path = os.path.join(SOURCE, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)


def tint(c, k):
    """k > 0 lightens towards white, k < 0 darkens."""
    if k >= 0:
        return tuple(int(v + (255 - v) * k) for v in c[:3])
    return tuple(int(v * (1 + k)) for v in c[:3])


def box(d, rect, color, radius=3, bevel=2):
    """A plastic block from above: lit top and left edge, shaded bottom and right edge."""
    x0, y0, x1, y1 = rect
    d.rounded_rectangle(rect, radius=radius, fill=tint(color, -0.35))
    d.rounded_rectangle((x0, y0, x1 - bevel, y1 - bevel), radius=radius, fill=tint(color, 0.3))
    d.rounded_rectangle((x0 + bevel, y0 + bevel, x1 - bevel, y1 - bevel), radius=max(0, radius - 1), fill=color)


def disc(d, cx, cy, r, color, shine=True):
    """A round knob / ball: darker rim at the bottom right, a highlight at the top left."""
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=tint(color, -0.3))
    d.ellipse((cx - r, cy - r, cx + r - 1, cy + r - 1), fill=color)
    if shine and r >= 3:
        h = max(1, r // 3)
        d.ellipse((cx - r // 2 - h, cy - r // 2 - h, cx - r // 2 + h, cy - r // 2 + h), fill=tint(color, 0.55))


def stud(d, cx, cy, color, r=3):
    """One toy-brick stud."""
    d.ellipse((cx - r, cy - r + 1, cx + r, cy + r + 1), fill=tint(color, -0.35))
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=tint(color, 0.15))
    d.point((cx - 1, cy - 1), fill=tint(color, 0.6))


def studs(d, rect, color, step=8, r=2):
    x0, y0, x1, y1 = rect
    for y in range(y0 + step // 2, y1, step):
        for x in range(x0 + step // 2, x1, step):
            stud(d, x, y, color, r)


def baseplate(img, d, color=BASE, studs_on=True):
    """The toy baseplate a factory stands on: fills the footprint, a few studs in each corner."""
    w, h = img.size
    box(d, (0, 0, w - 1, h - 1), color, radius=4, bevel=2)
    if studs_on:
        for cx, cy in ((5, 5), (w - 7, 5), (5, h - 7), (w - 7, h - 7)):
            stud(d, cx, cy, color, 2)


def wood(d, rect, color=WOOD, plank=6):
    """Planks running left-right with a darker seam and a little grain."""
    x0, y0, x1, y1 = rect
    d.rectangle(rect, fill=color)
    for y in range(y0, y1 + 1, plank):
        d.line((x0, y, x1, y), fill=tint(color, -0.3))
        for x in range(x0 + (y * 7) % 11, x1, 13):
            d.line((x, y + 2, x + 3, y + 2), fill=tint(color, -0.15))


def gear(d, cx, cy, r, color, teeth=8, hole=True):
    """A gear seen flat: teeth round a disc, an axle hole in the middle."""
    pts = []
    for i in range(teeth * 2):
        a = math.pi * i / teeth
        rr = r if i % 2 == 0 else r * 0.72
        for da in (-0.18, 0.18):
            pts.append((cx + rr * math.cos(a + da * math.pi / teeth), cy + rr * math.sin(a + da * math.pi / teeth)))
    d.polygon(pts, fill=color)
    d.ellipse((cx - r * 0.55, cy - r * 0.55, cx + r * 0.55, cy + r * 0.55), fill=tint(color, 0.2))
    if hole:
        d.ellipse((cx - r * 0.2, cy - r * 0.2, cx + r * 0.2, cy + r * 0.2), fill=INK)


def chute(d, w, h, color=YELLOW):
    """The output on the east side (buildings face east before they turn): a little striped hatch."""
    y0, y1 = h // 2 - 7, h // 2 + 7
    d.rectangle((w - 6, y0, w - 1, y1), fill=tint(color, -0.25))
    for y in range(y0 + 1, y1, 4):
        d.line((w - 5, y, w - 2, y + 2), fill=INK)
    d.polygon([(w - 9, h // 2 - 3), (w - 6, h // 2), (w - 9, h // 2 + 3)], fill=INK)


def mouse(img, cx, cy, size=1.0, hat=None, facing=-90, hat_color=YELLOW):
    """
    A mouse seen from above, nose towards `facing` degrees (0 = east, -90 = north): grey body, round ears
    with pink insides, a pink nose, a curly tail. hat: None, "hard" (builder), "straw" (farmer), "chef",
    "cap" (scout), "goggles".
    """
    s = size
    layer, d = canvas(*img.size)
    a = math.radians(facing)
    fx, fy = math.cos(a), math.sin(a)          # forwards
    sx, sy = -fy, fx                            # to the right

    def at(f, r):
        return cx + fx * f * s + sx * r * s, cy + fy * f * s + sy * r * s

    # tail: curls out behind
    tail = [at(-6 - i * 1.2, math.sin(i * 0.9) * 2.5) for i in range(7)]
    d.line(tail, fill=PINK, width=max(1, int(s)))
    # body and head
    bx, by = at(-1, 0)
    d.ellipse((bx - 5 * s, by - 5 * s, bx + 5 * s, by + 5 * s), fill=tint(MOUSE, -0.15))
    d.ellipse((bx - 4.5 * s, by - 4.5 * s, bx + 4 * s, by + 4 * s), fill=MOUSE)
    hx, hy = at(4, 0)
    for side in (-1, 1):
        ex, ey = at(3.5, side * 4)
        d.ellipse((ex - 2.6 * s, ey - 2.6 * s, ex + 2.6 * s, ey + 2.6 * s), fill=MOUSE)
        d.ellipse((ex - 1.4 * s, ey - 1.4 * s, ex + 1.4 * s, ey + 1.4 * s), fill=PINK)
    d.ellipse((hx - 3 * s, hy - 3 * s, hx + 3 * s, hy + 3 * s), fill=tint(MOUSE, 0.15))
    nx, ny = at(7, 0)
    d.ellipse((nx - 1 * s, ny - 1 * s, nx + 1 * s, ny + 1 * s), fill=PINK)
    # hat on the head
    if hat == "hard":
        d.ellipse((hx - 3.2 * s, hy - 3.2 * s, hx + 3.2 * s, hy + 3.2 * s), fill=tint(YELLOW, -0.2))
        d.ellipse((hx - 2.6 * s, hy - 2.8 * s, hx + 2.4 * s, hy + 2.2 * s), fill=YELLOW)
    elif hat == "straw":
        d.ellipse((hx - 4.5 * s, hy - 4.5 * s, hx + 4.5 * s, hy + 4.5 * s), fill=(214, 182, 96))
        d.ellipse((hx - 2.2 * s, hy - 2.2 * s, hx + 2.2 * s, hy + 2.2 * s), fill=RED)
    elif hat == "chef":
        d.ellipse((hx - 3.5 * s, hy - 3.5 * s, hx + 3.5 * s, hy + 3.5 * s), fill=WHITE)
        d.ellipse((hx - 1.5 * s, hy - 2.5 * s, hx + 1.5 * s, hy + 0.5 * s), fill=tint(WHITE, -0.12))
    elif hat == "cap":
        d.ellipse((hx - 3 * s, hy - 3 * s, hx + 3 * s, hy + 3 * s), fill=hat_color)
        px, py = at(6.5, 0)
        d.ellipse((px - 2 * s, py - 2 * s, px + 2 * s, py + 2 * s), fill=tint(hat_color, -0.25))
    elif hat == "goggles":
        for side in (-1, 1):
            gx, gy = at(5, side * 1.8)
            d.ellipse((gx - 1.4 * s, gy - 1.4 * s, gx + 1.4 * s, gy + 1.4 * s), fill=(120, 200, 230))
    img.alpha_composite(layer)


def part(w, h):
    """A canvas for a moving part (drawn centred on its anchor)."""
    return canvas(w, h)


# ---- mice (units): seen from above, nose to the east (the game turns them the way they go) ----

CELL = 48          # unit sheet cell (UI/UnitSheets.CellSize)
FEET = CELL - 6    # upright units: where the feet touch the ground (UnitSheets.FeetY)
FUR = MOUSE
BELLY = (214, 212, 222)
TAIL = (200, 100, 125)     # darker pink than the ears, so it reads against the floor

# walk steps: (left foot ahead, right foot ahead, arm swing, tail wiggle) in pixels along the nose
STRIDE = [(6, -12, -3, 2), (-4, -4, 0, 0), (-12, 6, 3, -2), (-4, -4, 0, 0)]
IDLE_FEET = (2, 2)       # standing: both feet under the body, toes just peeking out at the sides


def topdown_mouse(pose="idle", step=0, hat=None, shirt=None, held=None, act=None, extra=None, stripes=None):
    """
    One 48x48 cell: a mouse on two legs seen from above, nose to the east. Its two pink feet step out from
    under the body, the arms swing the other way, the pink tail wiggles behind. hat(d, hx, hy) on the head
    centre; shirt = body colour; held(d, x, y, pose, step) in the right hand (the south side); act(info)
    returns {"hand": (x, y)} for the action frames; extra(d, info) draws last; stripes = shirt stripes.
    """
    img, d = canvas(CELL, CELL)
    left, right, swing, wiggle = STRIDE[step] if pose == "walk" else (*IDLE_FEET, 0, 0)
    cy = CELL // 2
    bx = 23                       # body centre
    hx, hy = bx + 10, cy          # head centre
    body = shirt or FUR
    hand = (bx + 3 - swing, cy + 10)          # right hand (south side)
    back_hand = (bx + 3 + swing, cy - 10)     # left hand (north side)
    info = {"pose": pose, "step": step, "head": (hx, hy), "body": (bx, cy)}
    if pose == "act" and act:
        over = act(dict(info)) or {}
        hand = over.get("hand", hand)
        back_hand = over.get("back_hand", back_hand)
    info["hand"] = hand

    # tail behind, wiggling
    d.line([(bx - 7, cy), (bx - 12, cy + 2 + wiggle), (bx - 16, cy - 1 - wiggle), (bx - 20, cy + 2 + wiggle),
            (bx - 22, cy - 1)], fill=TAIL, width=3, joint="curve")
    # feet: the one behind steps out from under the body (left and right take turns), toes forward
    spread = 7 if pose != "walk" else 4
    for ahead, y in ((left, cy - spread), (right, cy + spread)):
        fx = bx + ahead
        d.line((bx - 2, y, fx + 1, y), fill=tint(FUR, -0.15), width=4)     # the leg
        d.ellipse((fx - 3, y - 3, fx + 5, y + 3), fill=PINK)
        d.point((fx + 4, y), fill=tint(PINK, -0.3))
    # arms and hands at the sides
    for x, y in (back_hand, hand):
        d.line((bx + 2, cy + (6 if y > cy else -6), x, y), fill=tint(body, -0.1), width=3)
    # body, with an optional striped shirt
    d.ellipse((bx - 8, cy - 9, bx + 8, cy + 9), fill=tint(body, -0.25))
    d.ellipse((bx - 8, cy - 9, bx + 7, cy + 8), fill=body)
    if stripes:
        mask, md = canvas(CELL, CELL)
        md.ellipse((bx - 7, cy - 8, bx + 6, cy + 7), fill=(255, 255, 255, 255))
        layer, ld = canvas(CELL, CELL)
        for x in range(bx - 7, bx + 7, 4):
            ld.rectangle((x, cy - 9, x + 1, cy + 9), fill=stripes)
        img.paste(layer, (0, 0), Image.composite(layer, Image.new("RGBA", layer.size), mask.split()[3]))
    for x, y in (back_hand, hand):
        disc(d, x, y, 2, FUR, shine=False)
    # head: round ears with pink insides on both sides, the snout forward, eyes, whiskers
    for side in (-1, 1):
        ex, ey = hx - 3, hy + 8 * side
        d.ellipse((ex - 5, ey - 5, ex + 5, ey + 5), fill=FUR)
        d.ellipse((ex - 3, ey - 3, ex + 3, ey + 3), fill=PINK)
    d.ellipse((hx - 7, hy - 7, hx + 7, hy + 7), fill=tint(FUR, -0.2))
    d.ellipse((hx - 7, hy - 7, hx + 6, hy + 6), fill=FUR)
    d.ellipse((hx + 3, hy - 4, hx + 11, hy + 4), fill=BELLY)
    d.ellipse((hx + 9, hy - 1, hx + 12, hy + 2), fill=PINK)
    for side in (-1, 1):
        d.ellipse((hx + 3, hy + 3 * side - 1, hx + 5, hy + 3 * side + 1), fill=INK)
        d.line((hx + 9, hy + 2 * side, hx + 14, hy + 5 * side), fill=tint(FUR, 0.4))
    if hat:
        hat(d, hx - 1, hy)
    if held:
        held(d, hand[0], hand[1], pose, step)
    if extra:
        extra(d, info)
    return img


def strip_sheet(cell_fn):
    """A one-row sheet (top-down units): idle, walk 0-3, act 0-1 (UI/UnitSheets)."""
    poses = [("idle", 0)] + [("walk", i) for i in range(4)] + [("act", i) for i in range(2)]
    sheet = Image.new("RGBA", (CELL * len(poses), CELL), (0, 0, 0, 0))
    for col, (pose, step) in enumerate(poses):
        sheet.alpha_composite(cell_fn(pose, step), (col * CELL, 0))
    return sheet


def unit_sheet(cell_fn, columns=7, cell=CELL):
    """Lays out the sheet: rows towards / away / side, columns idle, walk 0-3, act 0-1 (UI/UnitSheets)."""
    sheet = Image.new("RGBA", (cell * columns, cell * 3), (0, 0, 0, 0))
    poses = [("idle", 0)] + [("walk", i) for i in range(4)] + [("act", i) for i in range(2)]
    for row, facing in enumerate(("toward", "away", "side")):
        for col, (pose, step) in enumerate(poses[:columns]):
            sheet.alpha_composite(cell_fn(facing, pose, step), (col * cell, row * cell))
    return sheet
