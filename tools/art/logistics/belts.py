"""
Belts and the buildings that join them, drawn to fit together: yellow toy rails along both sides, a dark
rubber belt between. Every piece meets its neighbours at the tile edges with the same rails at the same
place, so a line of belts, splitters, sorters and junctions reads as one system. The moving treads are not
painted: a shader in the game draws them on the rubber (UI/BeltLook has the geometry).

Geometry (64 px tiles, facing east before turning; items run along y = 32):
  straight: rails y 14-17 and 46-49, rubber y 18-45
  curve:    a right turn west edge -> bottom edge round the corner (0, 64): rails r 14-17 and 46-49, rubber r 18-45

    python tools/art/logistics/belts.py && python tools/art/restyle.py <the files it lists>
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import *  # noqa: E402,F403

T = 64
RAIL = (246, 196, 72)
RUBBER = (84, 86, 104)
WRITTEN = []
OUTER, INNER = 50, 14          # band edges (distance from the belt's line / the curve's corner)
RAIL_W = 4


def out(img, rel):
    save(img, rel)
    WRITTEN.append(rel)


def straight_band(d, x0, x1, horizontal=True):
    """A straight piece of belt between x0 and x1 (or y0 and y1 when vertical), rails and rubber."""
    if horizontal:
        d.rectangle((x0, INNER, x1, OUTER - 1), fill=RUBBER)
        for y0 in (INNER, OUTER - RAIL_W):
            d.rectangle((x0, y0, x1, y0 + RAIL_W - 1), fill=RAIL)
            d.line((x0, y0, x1, y0), fill=tint(RAIL, 0.35))
        for x in range(x0 + 8, x1, 16):
            for y in (INNER + 1, OUTER - 3):
                d.point((x, y), fill=tint(RAIL, -0.45))      # bolts
    else:
        d.rectangle((INNER, x0, OUTER - 1, x1), fill=RUBBER)
        for y0 in (INNER, OUTER - RAIL_W):
            d.rectangle((y0, x0, y0 + RAIL_W - 1, x1), fill=RAIL)
            d.line((y0, x0, y0, x1), fill=tint(RAIL, 0.35))
        for x in range(x0 + 8, x1, 16):
            for y in (INNER + 1, OUTER - 3):
                d.point((y, x), fill=tint(RAIL, -0.45))


def straight():
    img, d = canvas(T, T)
    straight_band(d, 0, T - 1)
    out(img, "Conveyors/conveyor_straight.png")


def curve():
    img = Image.new("RGBA", (T, T), (0, 0, 0, 0))
    px = img.load()
    for y in range(T):
        for x in range(T):
            r = math.hypot(x + 0.5, T - (y + 0.5))
            if INNER <= r < OUTER:
                rail = r < INNER + RAIL_W or r >= OUTER - RAIL_W
                color = RAIL if rail else RUBBER
                if rail and (abs(r - INNER) < 1 or abs(r - (OUTER - RAIL_W)) < 1):
                    color = tint(RAIL, 0.35)
                px[x, y] = (*color, 255)
    d = ImageDraw.Draw(img)
    for angle in (22.5, 67.5):                     # bolts on both rails
        a = math.radians(angle)
        for r in (INNER + 1.5, OUTER - 2.5):
            d.point((r * math.cos(a), T - r * math.sin(a)), fill=tint(RAIL, -0.45))
    out(img, "Conveyors/conveyor_curve.png")


def stubs(d, sides=(0, 1, 2, 3)):
    """Belt stubs from the tile edges towards the middle box: 0 = east, 1 = south, 2 = west, 3 = north."""
    for side in sides:
        if side == 0:
            straight_band(d, T - INNER - 2, T - 1)
        elif side == 2:
            straight_band(d, 0, INNER + 1)
        elif side == 1:
            straight_band(d, T - INNER - 2, T - 1, horizontal=False)
        else:
            straight_band(d, 0, INNER + 1, horizontal=False)


def splitter():
    img, d = canvas(T, T)
    stubs(d)
    box(d, (12, 12, 51, 51), RED, radius=6)
    disc(d, 32, 32, 13, tint(RED, -0.35), shine=False)          # the wheel's well (the wheel is a part)
    out(img, "Buildings/splitter.png")

    wheel, w = part(28, 28)                                      # a paddle wheel throwing items left and right
    disc(w, 14, 14, 12, tint(GREY, 0.15))
    for k in range(4):
        a = k * math.pi / 2
        w.line((14, 14, 14 + 11 * math.cos(a), 14 + 11 * math.sin(a)), fill=tint(GREY, -0.35), width=3)
    disc(w, 14, 14, 3, YELLOW, shine=False)
    out(wheel, "Parts/splitter_wheel.png")


def sorter():
    img, d = canvas(T, T)
    stubs(d)
    box(d, (12, 12, 51, 51), BLUE, radius=6)
    d.rounded_rectangle((19, 19, 44, 44), radius=4, fill=tint(BLUE, 0.6))   # the window for the filter icon
    out(img, "Buildings/sorter.png")

    arm, a = part(30, 30)                                        # the flap that pushes the others aside
    a.rectangle((14, 2, 16, 15), fill=YELLOW)
    disc(a, 15, 15, 3, tint(GREY, -0.2), shine=False)
    out(arm, "Parts/sorter_arm.png")


def junction():
    img, d = canvas(T, T)
    straight_band(d, 0, T - 1)                                   # east-west underneath
    straight_band(d, 0, T - 1, horizontal=False)                 # north-south on top, a little bridge
    for y in (INNER - 2, OUTER + 1):
        d.line((INNER - 2, y, OUTER + 1, y), fill=tint(RAIL, -0.3), width=2)
    out(img, "Buildings/junction.png")


if __name__ == "__main__":
    straight()
    curve()
    splitter()
    sorter()
    junction()
    print(" ".join(WRITTEN))
