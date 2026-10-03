"""
Worker buildings (2x2 = 128 px, output east):
  toolbox   - a big open toolbox the builder mice live in: one saws a plank, one knocks with a mallet
  farmhouse - a dollhouse barn with a carrot patch: the pinwheel on the roof spins in the wind

    python tools/art/buildings/workers.py && python tools/art/restyle.py <the files it lists>
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import *  # noqa: E402,F403

WRITTEN = []


def out(img, rel):
    save(img, rel)
    WRITTEN.append(rel)


def toolbox():
    img, d = canvas(128, 128)
    baseplate(img, d)
    chute(d, 128, 128)
    # the open toolbox: the lid folded back at the top, the tray of tools inside, the handle
    box(d, (12, 8, 104, 30), tint(RED, -0.2), radius=4)
    box(d, (12, 26, 104, 84), RED, radius=4)
    d.rectangle((18, 32, 98, 78), fill=tint(RED, -0.45))
    d.rectangle((40, 14, 76, 20), fill=GREY)
    # tools in the tray: screwdriver, wrench, ruler, a few screws
    d.line((24, 40, 50, 40), fill=GREY, width=2)
    box(d, (50, 37, 62, 43), YELLOW, radius=2, bevel=1)
    d.line((24, 52, 56, 52), fill=GREY, width=3)
    d.ellipse((54, 47, 64, 57), fill=GREY)
    d.ellipse((57, 50, 61, 54), fill=tint(RED, -0.45))
    box(d, (66, 36, 92, 44), YELLOW, radius=1, bevel=1)
    for x in range(68, 92, 4):
        d.line((x, 37, x, 40), fill=INK)
    for x, y in ((72, 58), (80, 62), (88, 56), (76, 70)):
        disc(d, x, y, 2, GREY, shine=False)
    # the workshop floor in front: a plank on two trestles, the sawdust, two builder mice
    box(d, (20, 96, 72, 106), WOOD, radius=1, bevel=1)
    d.line((46, 96, 46, 106), fill=INK)
    for x, y in ((44, 110), (49, 112), (41, 113)):
        d.point((x, y), fill=YELLOW)
    mouse(img, 52, 116, 1.3, hat="hard", facing=-90)
    mouse(img, 96, 100, 1.3, hat="hard", facing=180)
    out(img, "Buildings/toolbox.png")

    saw, s = part(26, 10)
    s.polygon([(2, 2), (20, 2), (20, 7), (2, 5)], fill=tint(GREY, 0.3))
    for x in range(3, 20, 3):
        s.line((x, 6, x + 1, 8), fill=GREY)
    box(s, (19, 0, 25, 9), WOOD, radius=1, bevel=1)
    out(saw, "Parts/saw.png")


def farmhouse():
    img, d = canvas(128, 128)
    baseplate(img, d)
    chute(d, 128, 128)
    # the barn roof from above: two red slopes of planks, a white ridge, white trim
    box(d, (12, 10, 100, 86), WHITE, radius=3)
    for y0, y1, k in ((14, 47, 0.05), (49, 82, -0.12)):
        d.rectangle((16, y0, 96, y1), fill=tint(RED, k))
        for y in range(y0 + 4, y1, 5):
            d.line((16, y, 96, y), fill=tint(RED, k - 0.25))
    d.rectangle((14, 46, 98, 50), fill=WHITE)
    # the hay loft hatch, open, with hay
    box(d, (44, 20, 66, 40), tint(WOOD, -0.2), radius=1, bevel=1)
    d.rectangle((48, 24, 62, 36), fill=YELLOW)
    # the carrot patch with a little fence
    box(d, (14, 94, 92, 120), (120, 82, 56), radius=2)
    for x in range(20, 90, 9):
        for y in (101, 112):
            d.polygon([(x, y), (x + 4, y), (x + 2, y + 5)], fill=ORANGE)
            d.line((x + 2, y, x, y - 3), fill=GREEN)
            d.line((x + 2, y, x + 4, y - 3), fill=GREEN)
    for x in range(14, 93, 13):
        d.rectangle((x, 90, x + 2, 94), fill=WHITE)
    d.line((14, 91, 92, 91), fill=WHITE)
    mouse(img, 106, 104, 1.3, hat="straw", facing=180)
    # the pinwheel's stick on the corner of the roof
    d.line((104, 16, 104, 30), fill=WOOD, width=2)
    out(img, "Buildings/farmhouse.png")

    wheel, w = part(26, 26)
    for i, color in enumerate((RED, YELLOW, BLUE, GREEN)):
        cx, cy = 13, 13
        tips = [((cx, cy), (cx, 1), (cx + 9, 6)), ((cx, cy), (25, cy), (20, cy + 9)),
                ((cx, cy), (cx, 25), (cx - 9, 20)), ((cx, cy), (1, cy), (6, cy - 9))][i]
        w.polygon(tips, fill=color)
    disc(w, 13, 13, 2, WHITE, shine=False)
    out(wheel, "Parts/pinwheel.png")


if __name__ == "__main__":
    toolbox()
    farmhouse()
    print(" ".join(WRITTEN))
