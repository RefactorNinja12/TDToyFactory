"""
The extractors (1x1, face east = output on the right):
  brick   - a toy digger with a hard-hat mouse; the arm digs (part: digger_arm), bricks ride to the chute
  plastic - a gumball machine sucking up plastic beads (parts: gumball_beads spin, gumball_crank)
  battery - a toy magnet crane over battery cells (parts: magnet bobs, charge_light blinks)
  cheese  - a fondue pot on a toy stove, a chef mouse stirring (parts: fondue_spoon, fondue_bubble)

    python tools/art/buildings/extractors.py && python tools/art/restyle.py <the files it lists>
Part anchors (sprite px from the centre) live in Scripts/UI/BuildingParts.cs.
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


def brick_extractor():
    img, d = canvas(64, 64)
    baseplate(img, d)
    chute(d, 64, 64)
    # a little belt from the dig to the chute
    d.rectangle((34, 28, 57, 36), fill=DARK)
    for x in range(36, 57, 4):
        d.line((x, 29, x, 35), fill=tint(DARK, 0.25))
    # tracks
    for x0 in (14, 34):
        d.rounded_rectangle((x0, 38, x0 + 8, 58), radius=2, fill=DARK)
        for y in range(40, 57, 3):
            d.line((x0 + 1, y, x0 + 7, y), fill=tint(DARK, 0.3))
    # body and cab, the mouse driver
    box(d, (18, 36, 40, 56), YELLOW, radius=3)
    box(d, (21, 42, 37, 55), tint(YELLOW, 0.35), radius=2)
    mouse(img, 29, 50, 1.1, hat="hard", facing=-90)
    # the pivot the arm turns on
    disc(d, 29, 36, 4, GREY)
    out(img, "Buildings/extractor_brick.png")

    # arm part: pivot at the centre of a 40x40 canvas, reaching up (north) with the bucket
    arm, a = part(40, 40)
    box(a, (17, 4, 23, 20), tint(YELLOW, -0.15), radius=2, bevel=1)
    a.polygon([(13, 2), (27, 2), (25, 8), (15, 8)], fill=GREY)
    for x in (15, 19, 23):
        a.line((x, 0, x + 1, 2), fill=tint(GREY, -0.4))
    disc(a, 20, 20, 3, GREY)
    out(arm, "Parts/digger_arm.png")

    brick, b = part(10, 8)
    box(b, (0, 0, 9, 7), RED, radius=1, bevel=1)
    stud(b, 3, 3, RED, 1)
    stud(b, 7, 3, RED, 1)
    out(brick, "Parts/brick_ride.png")


def plastic_extractor():
    img, d = canvas(64, 64)
    baseplate(img, d)
    chute(d, 64, 64)
    # the hose sucking beads off the floor
    d.line([(14, 50), (10, 42), (14, 34), (22, 30)], fill=tint(GREEN, -0.3), width=5)
    disc(d, 13, 52, 5, tint(GREY, -0.2))
    # machine: a red base with a glass dome
    box(d, (18, 14, 50, 50), RED, radius=6)
    d.ellipse((21, 17, 47, 43), fill=GLASS)
    d.ellipse((24, 19, 31, 25), fill=tint(GLASS, 0.6))
    # the coin slot and the exit flap
    d.rectangle((46, 30, 52, 34), fill=tint(GREY, 0.2))
    d.rectangle((32, 45, 38, 49), fill=INK)
    out(img, "Buildings/extractor_plastic.png")

    beads, b = part(22, 22)
    colors = [YELLOW, BLUE, GREEN, ORANGE, PURPLE, PINK, WHITE, TEAL, RED]
    b.ellipse((1, 1, 20, 20), fill=tint(GLASS, 0.2))   # one filled disc: the outline only goes round it
    for i in range(22):
        ang = i * 2.39996
        r = 1.6 * math.sqrt(i + 0.5)
        x, y = 11 + r * math.cos(ang), 11 + r * math.sin(ang)
        b.ellipse((x - 2, y - 2, x + 1, y + 1), fill=colors[i % len(colors)])
    out(beads, "Parts/gumball_beads.png")

    crank, c = part(16, 16)
    c.line((8, 8, 8, 2), fill=GREY, width=2)
    disc(c, 8, 2, 2, YELLOW, shine=False)
    disc(c, 8, 8, 2, tint(GREY, -0.2), shine=False)
    out(crank, "Parts/gumball_crank.png")


def battery_extractor():
    img, d = canvas(64, 64)
    baseplate(img, d)
    chute(d, 64, 64)
    # a row of battery cells in the ground
    for i, x in enumerate((14, 24, 34)):
        box(d, (x, 34, x + 8, 54), CELL if i != 1 else tint(CELL, 0.2), radius=1, bevel=1)
        d.rectangle((x + 2, 32, x + 6, 34), fill=GREY)
        d.rectangle((x + 1, 36, x + 7, 40), fill=ORANGE)
    # the crane gantry over them
    box(d, (10, 10, 14, 30), BLUE, radius=1, bevel=1)
    box(d, (40, 10, 44, 30), BLUE, radius=1, bevel=1)
    box(d, (10, 10, 44, 15), BLUE, radius=1, bevel=1)
    d.line((27, 15, 27, 20), fill=GREY, width=1)
    # the control box, a goggled mouse at the lever
    box(d, (46, 40, 56, 52), tint(GREY, -0.1), radius=2)
    d.line((51, 40, 54, 36), fill=GREY, width=2)
    disc(d, 54, 35, 2, RED, shine=False)
    mouse(img, 52, 26, 1.0, hat="goggles", facing=90)
    out(img, "Buildings/extractor_battery.png")

    magnet, m = part(16, 16)
    m.arc((2, 2, 14, 14), 0, 180, fill=RED, width=4)
    m.rectangle((2, 4, 5, 8), fill=RED)
    m.rectangle((11, 4, 14, 8), fill=RED)
    m.rectangle((2, 8, 5, 10), fill=WHITE)
    m.rectangle((11, 8, 14, 10), fill=WHITE)
    m.line((8, 0, 8, 4), fill=GREY)
    out(magnet, "Parts/magnet.png")

    light, l = part(8, 8)
    disc(l, 4, 4, 3, (130, 236, 120), shine=False)
    out(light, "Parts/charge_light.png")


def cheese_melter():
    img, d = canvas(64, 64)
    baseplate(img, d)
    chute(d, 64, 64, color=CHEESE)
    # toy stove with knobs
    box(d, (12, 12, 50, 52), WHITE, radius=5)
    for x in (16, 22):
        disc(d, x, 48, 2, RED, shine=False)
    # the pot and the melted cheese
    disc(d, 31, 30, 15, ORANGE)
    d.ellipse((19, 18, 43, 42), fill=tint(ORANGE, -0.3))
    d.ellipse((21, 20, 41, 40), fill=CHEESE)
    d.ellipse((25, 23, 31, 27), fill=tint(CHEESE, 0.45))
    # the chef mouse at the stove's side
    mouse(img, 52, 15, 1.0, hat="chef", facing=180)
    out(img, "Buildings/cheese_melter.png")

    spoon, s = part(24, 24)   # centred on the pot: the spoon reaches out from the middle
    s.line((12, 12, 20, 6), fill=WOOD, width=2)
    disc(s, 12, 12, 3, tint(WOOD, 0.1), shine=False)
    out(spoon, "Parts/fondue_spoon.png")

    bubble, b = part(6, 6)
    b.ellipse((0, 0, 5, 5), fill=tint(CHEESE, 0.3))
    b.ellipse((1, 1, 3, 3), fill=tint(CHEESE, 0.7))
    out(bubble, "Parts/fondue_bubble.png")


if __name__ == "__main__":
    brick_extractor()
    plastic_extractor()
    battery_extractor()
    cheese_melter()
    print(" ".join(WRITTEN))
