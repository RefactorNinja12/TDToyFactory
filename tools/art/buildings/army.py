"""
Army factories (2x2 = 128 px, output east):
  soldier - a toy pirate ship on blue water: the sail billows, the flag flaps, cannons puff, waves roll by
  golem   - a toy crane stacking ABC blocks: the hook lowers a block, a mouse swings a mallet
  car     - a slot-car track round a paint booth: two cars lap it, a mouse waves the chequered flag

    python tools/art/buildings/army.py && python tools/art/restyle.py <the files it lists>
"""
import os
import sys

from PIL import ImageFont

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import *  # noqa: E402,F403

WRITTEN = []


def out(img, rel):
    save(img, rel)
    WRITTEN.append(rel)


def soldier_factory():
    """The pirate ship the plastic pirate mice are built in (bow east = the output side)."""
    img, d = canvas(128, 128)
    water = (70, 130, 200)
    baseplate(img, d, color=water)
    chute(d, 128, 128)
    # ripples on the water round the ship
    for x, y in ((14, 14), (40, 10), (90, 16), (20, 112), (70, 116), (104, 108)):
        d.arc((x, y, x + 12, y + 6), 200, 340, fill=tint(water, 0.35), width=1)
    # hull: a wooden toy boat seen from above, pointed bow to the east, a red stripe round the rail
    hull = [(14, 44), (86, 34), (114, 64), (86, 94), (14, 84)]
    d.polygon(hull, fill=tint(WOOD, -0.3))
    inner = [(20, 50), (84, 41), (104, 64), (84, 87), (20, 78)]
    d.polygon(inner, fill=WOOD)
    for y in range(46, 84, 6):
        d.line((20, y, 98 - abs(y - 64) // 2, y), fill=tint(WOOD, -0.18))
    d.line(hull + [hull[0]], fill=RED, width=2)
    # cannons poking out of both sides
    for x in (34, 54, 74):
        d.rectangle((x, 36 + (x - 34) // 8 - 6, x + 6, 40 + (x - 34) // 8), fill=DARK)
        d.rectangle((x, 88 - (x - 34) // 8, x + 6, 94 - (x - 34) // 8 + 4), fill=DARK)
    # the gangplank down to the output, a hatch, the mast foot and the crow's nest
    box(d, (104, 60, 124, 68), tint(WOOD, 0.15), radius=1, bevel=1)
    box(d, (26, 56, 40, 72), tint(WOOD, -0.25), radius=1, bevel=1)
    disc(d, 62, 64, 4, tint(WOOD, -0.4), shine=False)
    out(img, "Buildings/factory_soldier.png")

    sail, s_ = part(46, 36)             # the main sail, centred on the mast
    box(s_, (2, 2, 43, 33), WHITE, radius=2, bevel=1)
    s_.line((2, 18, 43, 18), fill=tint(WHITE, -0.2))
    disc(s_, 23, 15, 6, INK, shine=False)                    # the skull
    for ex in (21, 25):
        s_.point((ex, 14), fill=WHITE)
    s_.line((17, 24, 29, 28), fill=INK, width=2)             # crossed bones
    s_.line((17, 28, 29, 24), fill=INK, width=2)
    out(sail, "Parts/ship_sail.png")

    flag, f = part(22, 22)              # centre = the top of the mast
    f.line((11, 11, 11, 2), fill=GREY)
    f.rectangle((11, 2, 21, 9), fill=(60, 50, 60))
    f.point((16, 5), fill=WHITE)
    out(flag, "Parts/jolly_roger.png")

    wave, w = part(14, 6)
    w.arc((0, 0, 13, 6), 200, 340, fill=WHITE, width=2)
    out(wave, "Parts/wave.png")


def abc_block(d, x, y, size, color, letter):
    box(d, (x, y, x + size, y + size), color, radius=2)
    font = ImageFont.load_default()
    d.text((x + size // 2 - 3, y + size // 2 - 6), letter, fill=WHITE, font=font)


def golem_workshop():
    img, d = canvas(128, 128)
    baseplate(img, d)
    chute(d, 128, 128)
    # the stack the crane builds
    abc_block(d, 50, 74, 22, RED, "A")
    abc_block(d, 74, 74, 22, BLUE, "B")
    abc_block(d, 98, 86, 16, GREEN, "D")
    # the crane: a lattice mast in the corner, the boom along the top
    box(d, (12, 12, 26, 110), YELLOW, radius=2)
    for y in range(16, 108, 8):
        d.line((14, y, 24, y + 6), fill=tint(YELLOW, -0.4))
    box(d, (12, 12, 104, 22), YELLOW, radius=2)
    for x in range(16, 102, 8):
        d.line((x, 14, x + 6, 20), fill=tint(YELLOW, -0.4))
    box(d, (66, 22, 80, 28), tint(GREY, -0.1), radius=1, bevel=1)   # the trolley
    # the builder mouse with its mallet (a part)
    mouse(img, 38, 100, 1.4, hat="hard", facing=-45)
    out(img, "Buildings/factory_golem.png")

    hook, h = part(24, 40)              # rope from the top (under the trolley), the block at the bottom
    h.line((12, 0, 12, 18), fill=tint(GREY, 0.2), width=2)
    h.polygon([(8, 18), (16, 18), (12, 22)], fill=GREY)
    abc_block(h, 3, 21, 18, YELLOW, "C")
    out(hook, "Parts/crane_hook.png")

    mallet, m = part(24, 24)            # centre = the mouse's paw
    m.line((12, 12, 12, 2), fill=WOOD, width=2)
    box(m, (6, 0, 18, 6), RED, radius=1, bevel=1)
    out(mallet, "Parts/mallet.png")


def car_factory():
    img, d = canvas(128, 128)
    baseplate(img, d)
    chute(d, 128, 128)
    # the oval track with kerbs and a dashed middle line
    d.ellipse((10, 18, 118, 110), fill=tint(GREY, -0.35))
    d.ellipse((30, 38, 98, 90), fill=BASE)
    for i in range(0, 360, 20):
        d.arc((12, 20, 116, 108), i, i + 10, fill=RED, width=2)
    d.arc((20, 28, 108, 100), 0, 360, fill=WHITE, width=1)
    # the paint booth in the middle, a chequered finish flag stand
    box(d, (44, 48, 84, 80), BLUE, radius=3)
    d.rectangle((50, 54, 78, 74), fill=tint(BLUE, 0.35))
    d.line((52, 64, 76, 64), fill=WHITE, width=2)
    mouse(img, 112, 104, 1.3, hat="cap", facing=-135, hat_color=RED)
    out(img, "Buildings/factory_car.png")

    for name, color in (("race_car_red", RED), ("race_car_yellow", YELLOW)):
        car, c = part(18, 12)           # pointing east (the way it drives)
        box(c, (1, 1, 16, 10), color, radius=3, bevel=1)
        c.rectangle((7, 3, 11, 8), fill=(120, 190, 240))
        for x, y in ((3, 0), (13, 0), (3, 10), (13, 10)):
            c.rectangle((x - 1, y, x + 1, y + 1), fill=INK)
        out(car, f"Parts/{name}.png")

    flag, f = part(20, 20)              # centre = the paw holding the stick
    f.line((10, 10, 10, 1), fill=GREY, width=1)
    for i in range(3):
        for j in range(2):
            f.rectangle((11 + i * 2, 1 + j * 2, 12 + i * 2, 2 + j * 2), fill=WHITE if (i + j) % 2 else INK)
    out(flag, "Parts/race_flag.png")


if __name__ == "__main__":
    soldier_factory()
    golem_workshop()
    car_factory()
    print(" ".join(WRITTEN))
