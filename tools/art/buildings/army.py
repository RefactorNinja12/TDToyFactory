"""
Army factories (2x2 = 128 px, output east):
  soldier - a plastic-kit press: the press head stamps, a sprue of plastic pirate mice rides out to the chute
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
SOLDIER = (90, 168, 80)


def out(img, rel):
    save(img, rel)
    WRITTEN.append(rel)


def soldier_figure(d, x, y, color=SOLDIER):
    """A little plastic pirate mouse on the sprue, from above: round ears, a tricorn, shoulders."""
    for ex in (x - 3, x + 3):
        d.ellipse((ex - 2, y - 4, ex + 2, y), fill=tint(color, -0.1))
    d.ellipse((x - 3, y - 3, x + 3, y + 3), fill=tint(color, -0.15))
    d.polygon([(x - 4, y - 2), (x, y - 5), (x + 4, y - 2), (x, y)], fill=tint(color, -0.4))
    d.rectangle((x - 3, y + 2, x + 3, y + 6), fill=color)


def soldier_factory():
    img, d = canvas(128, 128)
    baseplate(img, d)
    chute(d, 128, 128)
    # the belt the sprues ride out on
    d.rectangle((24, 80, 121, 96), fill=DARK)
    for x in range(26, 121, 6):
        d.line((x, 81, x, 95), fill=tint(DARK, 0.25))
    # the press: two red pillars, a cross beam, the mould bed under the head
    box(d, (20, 14, 32, 76), RED, radius=2)
    box(d, (84, 14, 96, 76), RED, radius=2)
    box(d, (20, 10, 96, 22), tint(RED, -0.1), radius=3)
    box(d, (36, 50, 80, 76), tint(GREY, 0.2), radius=2)
    for x in (44, 58, 72):
        soldier_figure(d, x, 61, tint(GREY, -0.15))   # empty soldier-shaped hollows in the mould
    # control panel and the operator mouse
    box(d, (100, 20, 120, 44), tint(GREY, -0.1), radius=2)
    mouse(img, 110, 58, 1.4, hat="hard", facing=180)
    out(img, "Buildings/factory_soldier.png")

    head, h = part(48, 22)
    box(h, (0, 0, 47, 21), tint(GREY, -0.15), radius=3)
    for x in (6, 41):
        disc(h, x, 11, 2, tint(GREY, 0.3), shine=False)
    h.rectangle((14, 8, 33, 13), fill=tint(GREY, -0.4))
    out(head, "Parts/press_head.png")

    sprue, s = part(34, 16)
    s.rectangle((0, 0, 33, 15), outline=SOLDIER, width=2)
    s.line((0, 8, 33, 8), fill=SOLDIER)
    for x in (7, 17, 27):
        soldier_figure(s, x, 6)
    out(sprue, "Parts/sprue.png")

    light, l = part(8, 8)
    disc(l, 4, 4, 3, (250, 90, 80), shine=False)
    out(light, "Parts/panel_light.png")


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
