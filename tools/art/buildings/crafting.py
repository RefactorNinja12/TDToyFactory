"""
Crafting buildings (1x1, output east):
  assembler - a wind-up workbench: the big wind-up key turns, two gears mesh; the product icon sits on the
              tray in the middle (drawn by BuildingView)
  kitchen   - a play-kitchen stove: the pot lid rattles and steam puffs up, the oven glows

    python tools/art/buildings/crafting.py && python tools/art/restyle.py <the files it lists>
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import *  # noqa: E402,F403

WRITTEN = []


def out(img, rel):
    save(img, rel)
    WRITTEN.append(rel)


def assembler():
    img, d = canvas(64, 64)
    baseplate(img, d)
    chute(d, 64, 64)
    # the workbench, its tray for the product, a bolt in each corner
    box(d, (12, 8, 56, 56), WOOD, radius=3)
    wood(d, (15, 11, 52, 52), WOOD, plank=7)
    box(d, (21, 20, 43, 44), tint(GREY, 0.25), radius=4)
    for x, y in ((16, 12), (51, 12), (16, 51), (51, 51)):
        disc(d, x, y, 2, GREY, shine=False)
    # the key's socket on the west side
    box(d, (5, 26, 13, 38), tint(GREY, -0.1), radius=2, bevel=1)
    out(img, "Buildings/assembler.png")

    key, k = part(22, 22)          # centre = the axle
    k.ellipse((1, 4, 9, 18), fill=YELLOW)
    k.ellipse((13, 4, 21, 18), fill=YELLOW)
    k.ellipse((3, 7, 7, 15), fill=tint(YELLOW, -0.3))
    k.ellipse((15, 7, 19, 15), fill=tint(YELLOW, -0.3))
    k.rectangle((8, 9, 14, 13), fill=tint(YELLOW, -0.15))
    disc(k, 11, 11, 2, GREY, shine=False)
    out(key, "Parts/windup_key.png")

    big, b = part(22, 22)
    gear(b, 11, 11, 10, RED, teeth=9)
    out(big, "Parts/gear_big.png")
    small, s = part(16, 16)
    gear(s, 8, 8, 7, BLUE, teeth=6)
    out(small, "Parts/gear_small.png")


def kitchen():
    img, d = canvas(64, 64)
    baseplate(img, d)
    chute(d, 64, 64)
    # counter, two burners, the oven door with a glowing window
    box(d, (6, 6, 56, 58), (250, 140, 176), radius=5)
    for cx in (20, 42):
        d.ellipse((cx - 9, 13, cx + 9, 31), fill=DARK)
        d.ellipse((cx - 6, 16, cx + 6, 28), fill=tint(DARK, 0.25))
    box(d, (12, 36, 50, 54), WHITE, radius=3)
    d.rounded_rectangle((17, 40, 45, 50), radius=2, fill=(250, 150, 60))
    d.line((19, 42, 43, 42), fill=tint((250, 150, 60), 0.4))
    # the frying pan with an egg on the right burner
    disc(d, 42, 22, 7, tint(DARK, 0.1), shine=False)
    d.line((48, 26, 55, 31), fill=DARK, width=2)
    d.ellipse((38, 18, 46, 26), fill=WHITE)
    disc(d, 42, 22, 2, YELLOW, shine=False)
    # the pot on the left burner (its lid is a part)
    disc(d, 20, 22, 8, (90, 130, 210))
    d.rectangle((9, 20, 12, 24), fill=(90, 130, 210))
    d.rectangle((28, 20, 31, 24), fill=(90, 130, 210))
    out(img, "Buildings/kitchen.png")

    lid, l = part(16, 16)
    disc(l, 8, 8, 6, tint((90, 130, 210), 0.3))
    disc(l, 8, 8, 2, RED, shine=False)
    out(lid, "Parts/pot_lid.png")

    steam, s = part(12, 12)
    s.ellipse((1, 3, 8, 10), fill=WHITE)
    s.ellipse((4, 1, 11, 8), fill=WHITE)
    out(steam, "Parts/steam.png")


if __name__ == "__main__":
    assembler()
    kitchen()
    print(" ".join(WRITTEN))
