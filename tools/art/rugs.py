"""
Rugs on the nursery's floor (UI/FloorDecor puts one of each in each room), drawn flat from above at half size and
blown up with hard pixels. Pictures go to tools/art/source/Tiles; restyle.py gives them the night palette (no
outline, like the floor).

  Tiles/rug_round.png    320x320: a round rag rug, rings of colour round a cream star (5x5 tiles)
  Tiles/rug_striped.png  384x256: a striped rug with fringes at the ends (6x4 tiles)
  Tiles/rug_playmat.png  448x320: a play mat with roads, houses, trees and a pond (7x5 tiles)

    python tools/art/rugs.py && python tools/art/restyle.py <the files it lists>
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit import *  # noqa: E402,F403

T = 64
HALF = T // 2
WRITTEN = []
CREAM = (236, 226, 200)
RUG_COLOURS = [(76, 92, 150), (226, 196, 120), (196, 80, 80), (90, 150, 140)]   # navy, mustard, red, teal


def out(small, rel):
    """Saves the half-size picture blown up with hard pixels."""
    save(small.resize((small.width * 2, small.height * 2), Image.NEAREST), rel)
    WRITTEN.append(rel)


def round_rug():
    size = 5 * HALF
    img, d = canvas(size, size)
    c = size / 2
    navy, mustard, red, teal = RUG_COLOURS
    rings = [navy, mustard, red, CREAM, teal, mustard]
    for k, colour in enumerate(rings):
        r = c - 1 - k * (c - 12) / len(rings)
        d.ellipse((c - r, c - r, c + r, c + r), fill=colour)
        if k == 0:                                               # stitches round the edge
            for s in range(48):
                a = s * math.tau / 48
                d.point((c + (r - 2) * math.cos(a), c + (r - 2) * math.sin(a)), fill=CREAM)
    pts = []
    for k in range(10):                                          # a cream star in the middle
        a = -math.pi / 2 + k * math.pi / 5
        r = 11 if k % 2 == 0 else 5
        pts.append((c + r * math.cos(a), c + r * math.sin(a)))
    d.polygon(pts, fill=CREAM)
    out(img, "Tiles/rug_round.png")


def striped_rug():
    w, h = 6 * HALF, 4 * HALF
    img, d = canvas(w, h)
    fringe = 5
    d.rectangle((fringe, 0, w - 1 - fringe, h - 1), fill=CREAM)
    y, k = 6, 0
    while y < h - 8:                                             # bands across, a thin cream line between
        band = 10 if k % 2 == 0 else 4
        d.rectangle((fringe, y, w - 1 - fringe, y + band - 1), fill=RUG_COLOURS[(k + 2) % len(RUG_COLOURS)])
        y += band + 4
        k += 1
    d.rectangle((fringe, 0, w - 1 - fringe, h - 1), outline=(176, 150, 110))
    for x0 in (0, w - fringe):                                   # the fringes at both ends
        for y in range(2, h - 2, 3):
            d.line((x0, y, x0 + fringe - 1, y), fill=CREAM)
    out(img, "Tiles/rug_striped.png")


def play_mat():
    w, h = 7 * HALF, 5 * HALF
    rnd = random.Random(9)
    img, d = canvas(w, h)
    navy, mustard, red, _ = RUG_COLOURS
    d.rectangle((0, 0, w - 1, h - 1), fill=(110, 170, 96))
    d.rectangle((0, 0, w - 1, h - 1), outline=navy, width=3)
    road = (110, 110, 128)
    # a loop road round the mat and one across the middle
    d.rounded_rectangle((12, 12, w - 13, h - 13), radius=16, outline=road, width=9)
    d.rectangle((w // 2 - 4, 12, w // 2 + 4, h - 13), fill=road)
    for y in range(20, h - 20, 8):                               # dashes down the middle road
        d.line((w // 2, y, w // 2, y + 3), fill=CREAM)
    for x in range(30, w - 30, 8):                               # and along the top and bottom
        if abs(x - w // 2) > 6:
            d.line((x, 16, x + 3, 16), fill=CREAM)
            d.line((x, h - 17, x + 3, h - 17), fill=CREAM)
    # little houses, trees and a pond inside the loop
    for hx, hy, roof in ((34, 34, red), (60, 52, navy), (w - 60, 36, mustard)):
        d.rectangle((hx, hy, hx + 12, hy + 10), fill=CREAM)
        d.polygon([(hx - 2, hy), (hx + 6, hy - 7), (hx + 14, hy)], fill=roof)
        d.rectangle((hx + 5, hy + 5, hx + 7, hy + 10), fill=(122, 82, 54))
    for _ in range(7):
        tx, ty = rnd.randint(26, w // 2 - 14), rnd.randint(64, h - 30)
        d.ellipse((tx - 4, ty - 4, tx + 4, ty + 4), fill=(58, 107, 53))
    d.ellipse((w - 76, h - 62, w - 30, h - 32), fill=(82, 104, 160))      # palette blues: muted they stay blue
    d.ellipse((w - 68, h - 56, w - 50, h - 48), fill=(122, 149, 200))
    out(img, "Tiles/rug_playmat.png")


if __name__ == "__main__":
    round_rug()
    striped_rug()
    play_mat()
    print(" ".join(WRITTEN))
