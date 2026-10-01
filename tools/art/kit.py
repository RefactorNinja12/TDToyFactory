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
