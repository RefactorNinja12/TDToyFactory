"""
Walls and traps, seen from above: a red toy-brick wall, a gate (brick posts and a wooden door), an electric
fence (posts, striped wires, a battery box), the trapdoor (wooden lid with hinges and warning stripes, 2x2)
with its open hole as a part, and the pendulum (a rail on two stands, 4x1) with its weight as a part.
View/TrapView draws the open hole, the warning blink and the weight.

    python tools/art/defense/defense.py && python tools/art/restyle.py <the files it lists>
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import *  # noqa: E402,F403

STEEL = (150, 160, 186)
HOLE = (40, 34, 52)
BATTERY = (96, 108, 140)
WRITTEN = []


def out(img, rel):
    save(img, rel)
    WRITTEN.append(rel)


def brick_wall():
    img, d = canvas(64, 64)
    box(d, (2, 4, 61, 59), RED, radius=4, bevel=3)
    for y in (18, 44):
        for x in (17, 46):
            stud(d, x, y, RED, 7)
    d.line((4, 31, 59, 31), fill=tint(RED, -0.25))
    out(img, "Defense/brick_wall.png")


def gate():
    img, d = canvas(64, 64)
    for x0 in (2, 50):
        box(d, (x0, 4, x0 + 11, 59), RED, radius=3, bevel=2)
        stud(d, x0 + 6, 16, RED, 4)
        stud(d, x0 + 6, 46, RED, 4)
    wood(d, (14, 14, 49, 49), plank=7)
    for y in (20, 42):
        d.rectangle((12, y, 16, y + 3), fill=DARK)
    disc(d, 44, 32, 3, YELLOW)
    out(img, "Defense/gate.png")


def electric_fence():
    img, d = canvas(64, 64)
    for y in (22, 32, 42):
        for x in range(6, 58, 8):
            d.line((x, y, x + 4, y), fill=YELLOW, width=2)
            d.line((x + 4, y, x + 8, y), fill=INK, width=2)
    for x in (4, 56):
        box(d, (x - 4, 14, x + 4, 50), GREY, radius=2, bevel=1)
        disc(d, x, 14, 3, WHITE)
    box(d, (24, 46, 40, 60), BATTERY, radius=2, bevel=1)
    d.rectangle((30, 44, 34, 46), fill=YELLOW)
    d.polygon([(33, 49), (29, 54), (32, 54), (30, 58), (35, 52), (32, 52)], fill=YELLOW)
    out(img, "Defense/electric_fence.png")


def stripes(d, rect, width=8):
    x0, y0, x1, y1 = rect
    for k in range(x0 - (y1 - y0), x1, width * 2):
        d.polygon([(k, y1), (k + width, y1), (k + width + (y1 - y0), y0), (k + (y1 - y0), y0)], fill=YELLOW)


def trapdoor():
    img, d = canvas(128, 128)
    box(d, (0, 0, 127, 127), DARK, radius=4, bevel=2)
    border = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    bd = ImageDraw.Draw(border)
    bd.rectangle((0, 0, 127, 127), fill=INK)
    stripes(bd, (0, 0, 127, 127), 9)
    mask = Image.new("L", (128, 128), 0)
    ImageDraw.Draw(mask).rectangle((4, 4, 123, 123), fill=255)
    ImageDraw.Draw(mask).rectangle((14, 14, 113, 113), fill=0)
    img.paste(border, (0, 0), mask)
    wood(d, (14, 14, 113, 113), plank=10)
    d.line((14, 64, 113, 64), fill=tint(WOOD, -0.45), width=2)
    for x in (30, 98):
        for y in (16, 104):
            d.rectangle((x - 6, y, x + 6, y + 8), fill=GREY)
    disc(d, 64, 56, 4, GREY)
    disc(d, 64, 72, 4, GREY)
    out(img, "Defense/trapdoor.png")

    hole, hd = canvas(100, 100)
    hd.rectangle((0, 0, 99, 99), fill=HOLE)
    hd.rectangle((0, 0, 99, 10), fill=tint(HOLE, -0.4))
    out(hole, "Parts/trapdoor_open.png")

    warn, wd = canvas(128, 128)
    wd.rectangle((0, 0, 127, 127), fill=INK)
    stripes(wd, (0, 0, 127, 127), 9)
    m = Image.new("L", (128, 128), 0)
    ImageDraw.Draw(m).rectangle((0, 0, 127, 127), fill=255)
    ImageDraw.Draw(m).rectangle((12, 12, 115, 115), fill=0)
    blink = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    blink.paste(Image.new("RGBA", (128, 128), RED + (255,)), (0, 0), m)
    out(blink, "Parts/trapdoor_warn.png")


def pendulum():
    img, d = canvas(256, 64)
    for x0 in (4, 228):
        box(d, (x0, 10, x0 + 23, 53), WOOD, radius=3, bevel=2)
        stud(d, x0 + 12, 32, WOOD, 5)
    for y in (26, 36):
        d.rectangle((16, y, 240, y + 2), fill=STEEL)
        d.line((16, y, 240, y), fill=tint(STEEL, 0.4))
    for x in range(30, 230, 16):
        d.line((x, 28, x, 36), fill=tint(STEEL, -0.3), width=2)
    out(img, "Defense/pendulum.png")

    bob, bd = canvas(44, 44)
    bd.rectangle((20, 0, 23, 12), fill=GREY)
    disc(bd, 22, 24, 17, ORANGE)
    stud(bd, 22, 24, ORANGE, 6)
    out(bob, "Parts/pendulum_bob.png")


if __name__ == "__main__":
    brick_wall()
    gate()
    electric_fence()
    trapdoor()
    pendulum()
    print(" ".join(WRITTEN))
