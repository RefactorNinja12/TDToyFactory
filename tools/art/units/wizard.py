"""
The wizard: a cute old mouse, upright like the golem (64 px cells; rows towards / away / side, the game mirrors the
side for left). Blue robe, pointy hat with a star, a long white beard, round glasses and a cane; big round ears poke out
under the brim and a thin tail curls out under the robe. Walk: the robe sways, the cane taps. Act (casting): the cane
raised high, then pointed forwards with a spark at its tip.

    python tools/art/units/wizard.py && python tools/art/restyle.py <the files it lists>
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from kit import *  # noqa: E402,F403

C = 64
FEET = C - 6
ROBE = (66, 104, 214)
HAT = (58, 88, 196)
BEARD = (246, 248, 252)
CANE = (150, 96, 54)
SWAY = [0, 2, 0, -2]  # the robe's hem, per walk step


def star(d, cx, cy, r, color=YELLOW):
    d.polygon([(cx, cy - r), (cx + r // 3, cy - r // 3), (cx + r, cy), (cx + r // 3, cy + r // 3),
               (cx, cy + r), (cx - r // 3, cy + r // 3), (cx - r, cy), (cx - r // 3, cy - r // 3)], fill=color)


def wizard(facing, pose, step):
    img, d = canvas(C, C)
    side = facing == "side"
    sway = SWAY[step] if pose == "walk" else 0
    cx = C // 2 - (2 if side else 0)
    head_x = cx + (4 if side else 0)  # a slight stoop: the head ahead of the body in profile
    head_y = 25
    # feet peeping out under the hem, stepping in turn
    lift = (step % 2) * 2 if pose == "walk" else 0
    for i, fx in enumerate((cx - 5, cx + 4) if not side else (cx - 1, cx + 5)):
        d.ellipse((fx - 3, FEET - 3 - (lift if i == step % 2 else 0), fx + 3, FEET + 1), fill=PINK)
    # the tail curls out under the robe at the back
    tail_x = cx - 13 if side else cx + 12
    d.line((cx + (-6 if side else 6), FEET - 4, tail_x, FEET - 8, tail_x + (-3 if side else 3), FEET - 14), fill=PINK, width=2)
    # the robe: wider at the hem, swaying while it walks
    top = 31
    d.polygon([(cx - 8, top), (cx + 8, top), (cx + 12 + sway, FEET - 3), (cx - 12 + sway, FEET - 3)], fill=ROBE)
    d.line((cx - 12 + sway, FEET - 3, cx + 12 + sway, FEET - 3), fill=tint(ROBE, -0.3), width=2)
    d.line((cx, top + 2, cx + sway // 2, FEET - 4), fill=tint(ROBE, -0.15))
    # head
    d.ellipse((head_x - 8, head_y - 8, head_x + 8, head_y + 7), fill=MOUSE)
    if facing != "away":
        nose_x = head_x + (9 if side else 0)
        # the beard: long and white, hanging from under the nose
        if side:
            d.polygon([(head_x + 2, head_y + 2), (head_x + 9, head_y + 3), (head_x + 6, head_y + 20)], fill=BEARD)
        else:
            d.polygon([(head_x - 7, head_y + 1), (head_x + 7, head_y + 1), (head_x + 1, head_y + 21)], fill=BEARD)
        d.ellipse((nose_x - 2, head_y - 1, nose_x + 2, head_y + 2), fill=PINK)
        # whiskers
        for dy in (-1, 2):
            d.line((nose_x - (0 if side else 9), head_y + dy, nose_x + 7, head_y + dy - 1), fill=GREY)
        # round glasses and small kind eyes
        for gx in ([head_x + 4] if side else [head_x - 4, head_x + 4]):
            d.ellipse((gx - 3, head_y - 5, gx + 3, head_y + 1), outline=YELLOW)
            d.point((gx, head_y - 2), fill=INK)
    # the pointy hat, its tip bent back a little, with a star
    brim_y = head_y - 8
    d.ellipse((head_x - 12, brim_y - 3, head_x + 12, brim_y + 3), fill=tint(HAT, -0.2))
    tip = (head_x - 6, 2) if side else (head_x + 5, 2)
    d.polygon([(head_x - 9, brim_y), (head_x + 9, brim_y), tip], fill=HAT)
    star(d, head_x + (0 if not side else -1), brim_y - 8, 4)
    # big round ears poking out under the brim
    ears = [(head_x - 5, head_y - 3)] if side else [(head_x - 12, head_y - 3), (head_x + 12, head_y - 3)]
    for ex, ey in ears:
        disc(d, ex, ey, 5, MOUSE, shine=False)
        disc(d, ex, ey, 3, PINK, shine=False)
    # the cane and the hand holding it
    if pose == "act" and step == 0:  # raised high over the hat
        hand, top_end, bottom_end = (cx + 11, 18), (cx + 13, 1), (cx + 9, 34)
    elif pose == "act":  # pointed forwards, a spark at the tip
        hand, top_end, bottom_end = (cx + 12, 32), (cx + 28, 22), (cx + 6, 36)
    else:  # leaning on it, tapping it down while walking
        tap = -2 if pose == "walk" and step in (1, 3) else 0
        x = cx + (14 if not side else 15)
        hand, top_end, bottom_end = (x, 36 + tap), (x, 30 + tap), (x, FEET + tap)
    d.line((*bottom_end, *top_end), fill=CANE, width=3)
    disc(d, top_end[0], top_end[1], 3, tint(CANE, 0.25), shine=False)
    disc(d, hand[0], hand[1], 3, MOUSE, shine=False)
    if pose == "act" and step == 1:
        star(d, top_end[0] + 3, top_end[1] - 2, 5, (250, 240, 140))
    return img


def main():
    save(unit_sheet(wizard, cell=C), "Units/wizard_sheet.png")
    save(wizard("toward", "idle", 0), "Units/wizard.png")
    print("Units/wizard_sheet.png Units/wizard.png")


if __name__ == "__main__":
    main()
